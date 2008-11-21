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

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Xml;
using System.Threading;
using System.Timers;
using OpenMetaverse;
using OpenMetaverse.Imaging;
using OpenMetaverse.Packets;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Servers;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Interfaces;
using OpenSim.Region.Environment.Modules.World.Archiver;
using OpenSim.Region.Environment.Modules.World.Serialiser;
using OpenSim.Region.Environment.Modules.World.Terrain;
using OpenSim.Region.Environment.Scenes.Scripting;
using OpenSim.Region.Physics.Manager;
using Nini.Config;
using Caps = OpenSim.Framework.Communications.Capabilities.Caps;
using Image = System.Drawing.Image;
using TPFlags = OpenSim.Framework.Constants.TeleportFlags;
using Timer = System.Timers.Timer;

namespace OpenSim.Region.Environment.Scenes
{
    public delegate bool FilterAvatarList(ScenePresence avatar);

    public partial class Scene : SceneBase
    {
        public delegate void SynchronizeSceneHandler(Scene scene);
        public SynchronizeSceneHandler SynchronizeScene = null;
        public int splitID = 0;

        private const long DEFAULT_MIN_TIME_FOR_PERSISTENCE = 60L;
        private const long DEFAULT_MAX_TIME_FOR_PERSISTENCE = 600L;

        #region Fields

        protected Timer m_restartWaitTimer = new Timer();

        protected SimStatsReporter m_statsReporter;

        protected List<RegionInfo> m_regionRestartNotifyList = new List<RegionInfo>();
        protected List<RegionInfo> m_neighbours = new List<RegionInfo>();

        /// <value>
        /// The scene graph for this scene
        /// </value>
        /// TODO: Possibly stop other classes being able to manipulate this directly.
        public SceneGraph m_sceneGraph;

        private int m_timePhase = 24;

        /// <summary>
        /// Are we applying physics to any of the prims in this scene?
        /// </summary>
        public bool m_physicalPrim;
        public float m_maxNonphys = 65536;
        public float m_maxPhys = 10;
        public bool m_clampPrimSize = false;

        public bool m_seeIntoRegionFromNeighbor;
        public int MaxUndoCount = 5;
        private int m_RestartTimerCounter;
        private readonly Timer m_restartTimer = new Timer(15000); // Wait before firing
        private int m_incrementsof15seconds = 0;
        private volatile bool m_backingup = false;

        private Dictionary<UUID, ReturnInfo> m_returns = new Dictionary<UUID, ReturnInfo>();

        protected string m_simulatorVersion = "OpenSimulator Server";

        protected ModuleLoader m_moduleLoader;
        protected StorageManager m_storageManager;
        protected AgentCircuitManager m_authenticateHandler;
        public CommunicationsManager CommsManager;

        protected SceneCommunicationService m_sceneGridService;

        public SceneCommunicationService SceneGridService
        {
            get { return m_sceneGridService; }
        }

        /// <summary>
        /// Each agent has its own capabilities handler.
        /// </summary>
        protected Dictionary<UUID, Caps> m_capsHandlers = new Dictionary<UUID, Caps>();

        protected BaseHttpServer m_httpListener;

        protected Dictionary<string, IRegionModule> m_modules = new Dictionary<string, IRegionModule>();
        public Dictionary<string, IRegionModule> Modules
        {
            get { return m_modules; }
        }
        protected Dictionary<Type, List<object> > ModuleInterfaces = new Dictionary<Type, List<object> >();
        protected Dictionary<string, object> ModuleAPIMethods = new Dictionary<string, object>();
        protected Dictionary<string, ICommander> m_moduleCommanders = new Dictionary<string, ICommander>();

        //API module interfaces

        public IXfer XferManager;

        protected IHttpRequests m_httpRequestModule;
        protected IXMLRPC m_xmlrpcModule;
        protected IWorldComm m_worldCommModule;
        protected IAvatarFactory m_AvatarFactory;
        protected IConfigSource m_config;
        protected IRegionArchiver m_archiver;
        protected IRegionSerialiser m_serialiser;

        // Central Update Loop

        protected int m_fps = 10;
        protected int m_frame = 0;
        protected float m_timespan = 0.089f;
        protected DateTime m_lastupdate = DateTime.Now;

        protected float m_timedilation = 1.0f;

        private int m_update_physics = 1;
        private int m_update_entitymovement = 1;
        private int m_update_entities = 1; // Run through all objects checking for updates
        private int m_update_entitiesquick = 200; // Run through objects that have scheduled updates checking for updates
        private int m_update_presences = 1; // Update scene presence movements
        private int m_update_events = 1;
        private int m_update_backup = 200;
        private int m_update_terrain = 50;
        private int m_update_land = 1;

        private int frameMS = 0;
        private int physicsMS2 = 0;
        private int physicsMS = 0;
        private int otherMS = 0;

        private bool m_physics_enabled = true;
        private bool m_scripts_enabled = true;
        private string m_defaultScriptEngine;
        private int m_LastLogin = 0;
        private Thread HeartbeatThread;
        private volatile bool shuttingdown = false;

        private object m_deleting_scene_object = new object();

        // the minimum time that must elapse before a changed object will be considered for persisted
        public long m_dontPersistBefore = DEFAULT_MIN_TIME_FOR_PERSISTENCE * 10000000L;
        // the maximum time that must elapse before a changed object will be considered for persisted
        public long m_persistAfter = DEFAULT_MAX_TIME_FOR_PERSISTENCE * 10000000L;

        #endregion

        #region Properties

        public AgentCircuitManager AuthenticateHandler
        {
            get { return m_authenticateHandler; }
        }

        // an instance to the physics plugin's Scene object.
        public PhysicsScene PhysicsScene
        {
            set { m_sceneGraph.PhysicsScene = value; }
            get { return m_sceneGraph.PhysicsScene; }
        }

        // This gets locked so things stay thread safe.
        public object SyncRoot
        {
            get { return m_sceneGraph.m_syncRoot; }
        }

        public float TimeDilation
        {
            get { return m_timedilation; }
        }

        /// <summary>
        /// This is for llGetRegionFPS
        /// </summary>
        public float SimulatorFPS
        {
            get { return m_statsReporter.getLastReportedSimFPS(); }
        }

        public int TimePhase
        {
            get { return m_timePhase; }
        }

        public string DefaultScriptEngine
        {
            get { return m_defaultScriptEngine; }
        }

        // Local reference to the objects in the scene (which are held in the scenegraph)
        //        public Dictionary<UUID, SceneObjectGroup> Objects
        //        {
        //            get { return m_sceneGraph.SceneObjects; }
        //        }

        // Reference to all of the agents in the scene (root and child)
        protected Dictionary<UUID, ScenePresence> m_scenePresences
        {
            get { return m_sceneGraph.ScenePresences; }
            set { m_sceneGraph.ScenePresences = value; }
        }

        //        protected Dictionary<UUID, SceneObjectGroup> m_sceneObjects
        //        {
        //            get { return m_sceneGraph.SceneObjects; }
        //            set { m_sceneGraph.SceneObjects = value; }
        //        }

        /// <summary>
        /// The dictionary of all entities in this scene.  The contents of this dictionary may be changed at any time.
        /// If you wish to add or remove entities, please use the appropriate method for that entity rather than
        /// editing this dictionary directly.
        ///
        /// If you want a list of entities where the list itself is guaranteed not to change, please use
        /// GetEntities()
        /// </summary>
        public Dictionary<UUID, EntityBase> Entities
        {
            get { return m_sceneGraph.Entities; }
            set { m_sceneGraph.Entities = value; }
        }

        public Dictionary<UUID, ScenePresence> m_restorePresences
        {
            get { return m_sceneGraph.RestorePresences; }
            set { m_sceneGraph.RestorePresences = value; }
        }

        public int objectCapacity = 45000;

        #endregion

        #region Constructors

        public Scene(RegionInfo regInfo, AgentCircuitManager authen,
                     CommunicationsManager commsMan, SceneCommunicationService sceneGridService,
                     AssetCache assetCach, StorageManager storeManager, BaseHttpServer httpServer,
                     ModuleLoader moduleLoader, bool dumpAssetsToFile, bool physicalPrim,
                     bool SeeIntoRegionFromNeighbor, IConfigSource config, string simulatorVersion)
        {
            m_config = config;

            m_moduleLoader = moduleLoader;
            m_authenticateHandler = authen;
            CommsManager = commsMan;
            m_sceneGridService = sceneGridService;
            m_storageManager = storeManager;
            AssetCache = assetCach;
            m_regInfo = regInfo;
            m_regionHandle = m_regInfo.RegionHandle;
            m_regionName = m_regInfo.RegionName;
            m_datastore = m_regInfo.DataStore;

            m_physicalPrim = physicalPrim;
            m_seeIntoRegionFromNeighbor = SeeIntoRegionFromNeighbor;

            m_eventManager = new EventManager();
            m_externalChecks = new SceneExternalChecks(this);
            
            m_asyncSceneObjectDeleter = new AsyncSceneObjectGroupDeleter(this);
            m_asyncSceneObjectDeleter.Enabled = true;

            // Load region settings
            m_regInfo.RegionSettings = m_storageManager.DataStore.LoadRegionSettings(m_regInfo.RegionID);
            if (m_storageManager.EstateDataStore != null)
                m_regInfo.EstateSettings = m_storageManager.EstateDataStore.LoadEstateSettings(m_regInfo.RegionID);

            //Bind Storage Manager functions to some land manager functions for this scene
            EventManager.OnLandObjectAdded +=
                new EventManager.LandObjectAdded(m_storageManager.DataStore.StoreLandObject);
            EventManager.OnLandObjectRemoved +=
                new EventManager.LandObjectRemoved(m_storageManager.DataStore.RemoveLandObject);

            m_sceneGraph = new SceneGraph(this, m_regInfo);

            // If the scene graph has an Unrecoverable error, restart this sim.
            // Currently the only thing that causes it to happen is two kinds of specific
            // Physics based crashes.
            //
            // Out of memory
            // Operating system has killed the plugin
            m_sceneGraph.UnRecoverableError += RestartNow;

            RegisterDefaultSceneEvents();

            m_httpListener = httpServer;
            m_dumpAssetsToFile = dumpAssetsToFile;

            m_scripts_enabled = !RegionInfo.RegionSettings.DisableScripts;

            m_physics_enabled = !RegionInfo.RegionSettings.DisablePhysics;

            m_statsReporter = new SimStatsReporter(this);
            m_statsReporter.OnSendStatsResult += SendSimStatsPackets;

            m_statsReporter.SetObjectCapacity(objectCapacity);

            m_simulatorVersion = simulatorVersion
                + " (OS " + Util.GetOperatingSystemInformation() + ")"
                + " ChilTasks:" + m_seeIntoRegionFromNeighbor.ToString()
                + " PhysPrim:" + m_physicalPrim.ToString();

            try
            {
                IConfig startupConfig = m_config.Configs["Startup"];
                m_maxNonphys = startupConfig.GetFloat("NonPhysicalPrimMax", 65536.0f);
                m_maxPhys = startupConfig.GetFloat("PhysicalPrimMax", 10.0f);
                m_clampPrimSize = startupConfig.GetBoolean("ClampPrimSize", false);
                m_dontPersistBefore =
                  startupConfig.GetLong("MinimumTimeBeforePersistenceConsidered", DEFAULT_MIN_TIME_FOR_PERSISTENCE);
                m_dontPersistBefore *= 10000000;
                m_persistAfter =
                  startupConfig.GetLong("MaximumTimeBeforePersistenceConsidered", DEFAULT_MAX_TIME_FOR_PERSISTENCE);
                m_persistAfter *= 10000000;

                m_defaultScriptEngine = startupConfig.GetString("DefaultScriptEngine", "DotNetEngine");
            }
            catch
            {
                m_log.Warn("[SCENE]: Failed to load StartupConfig");
            }
        }

        #endregion

        #region Startup / Close Methods

        public bool ShuttingDown
        {
            get { return shuttingdown; }
        }

        protected virtual void RegisterDefaultSceneEvents()
        {
            m_eventManager.OnPermissionError += SendPermissionAlert;
        }

        public override string GetSimulatorVersion()
        {
            return m_simulatorVersion;
        }

        /// <summary>
        /// Another region is up. Gets called from Grid Comms:
        /// (OGS1 -> LocalBackEnd -> RegionListened -> SceneCommunicationService)
        /// We have to tell all our ScenePresences about it, and add it to the
        /// neighbor list.
        ///
        /// We only add it to the neighbor list if it's within 1 region from here.
        /// Agents may have draw distance values that cross two regions though, so
        /// we add it to the notify list regardless of distance. We'll check
        /// the agent's draw distance before notifying them though.
        /// </summary>
        /// <param name="otherRegion">RegionInfo handle for the new region.</param>
        /// <returns>True after all operations complete, throws exceptions otherwise.</returns>
        public override bool OtherRegionUp(RegionInfo otherRegion)
        {
            if (RegionInfo.RegionHandle != otherRegion.RegionHandle)
            {
                for (int i = 0; i < m_neighbours.Count; i++)
                {
                    // The purpose of this loop is to re-update the known neighbors
                    // when another region comes up on top of another one.
                    // The latest region in that location ends up in the
                    // 'known neighbors list'
                    // Additionally, the commFailTF property gets reset to false.
                    if (m_neighbours[i].RegionHandle == otherRegion.RegionHandle)
                    {
                        lock (m_neighbours)
                        {
                            m_neighbours[i] = otherRegion;
                        }
                    }
                }

                // If the value isn't in the neighbours, add it.
                // If the RegionInfo isn't exact but is for the same XY World location,
                // then the above loop will fix that.

                if (!(CheckNeighborRegion(otherRegion)))
                {
                    lock (m_neighbours)
                    {
                        m_neighbours.Add(otherRegion);
                        //m_log.Info("[UP]: " + otherRegion.RegionHandle.ToString());
                    }
                }

                // If these are cast to INT because long + negative values + abs returns invalid data
                int resultX = Math.Abs((int)otherRegion.RegionLocX - (int)RegionInfo.RegionLocX);
                int resultY = Math.Abs((int)otherRegion.RegionLocY - (int)RegionInfo.RegionLocY);
                if (resultX <= 1 && resultY <= 1)
                {
                    try
                    {
                        ForEachScenePresence(delegate(ScenePresence agent)
                                             {
                                                 // If agent is a root agent.
                                                 if (!agent.IsChildAgent)
                                                 {
                                                     //agent.ControllingClient.new
                                                     //this.CommsManager.InterRegion.InformRegionOfChildAgent(otherRegion.RegionHandle, agent.ControllingClient.RequestClientInfo());
                                                     InformClientOfNeighbor(agent, otherRegion);
                                                 }
                                             }
                            );
                    }
                    catch (NullReferenceException)
                    {
                        // This means that we're not booted up completely yet.
                        // This shouldn't happen too often anymore.
                        m_log.Error("[SCENE]: Couldn't inform client of regionup because we got a null reference exception");
                    }
                }
                else
                {
                    m_log.Info("[INTERGRID]: Got notice about far away Region: " + otherRegion.RegionName.ToString() +
                               " at  (" + otherRegion.RegionLocX.ToString() + ", " +
                               otherRegion.RegionLocY.ToString() + ")");
                }
            }
            return true;
        }

        public void AddNeighborRegion(RegionInfo region)
        {
            lock (m_neighbours)
            {
                if (!CheckNeighborRegion(region))
                {
                    m_neighbours.Add(region);
                }
            }
        }

        public bool CheckNeighborRegion(RegionInfo region)
        {
            bool found = false;
            lock (m_neighbours)
            {
                foreach (RegionInfo reg in m_neighbours)
                {
                    if (reg.RegionHandle == region.RegionHandle)
                    {
                        found = true;
                        break;
                    }
                }
            }
            return found;
        }

        /// <summary>
        /// Given float seconds, this will restart the region.
        /// </summary>
        /// <param name="seconds">float indicating duration before restart.</param>
        public virtual void Restart(float seconds)
        {
            // notifications are done in 15 second increments
            // so ..   if the number of seconds is less then 15 seconds, it's not really a restart request
            // It's a 'Cancel restart' request.

            // RestartNow() does immediate restarting.
            if (seconds < 15)
            {
                m_restartTimer.Stop();
                SendGeneralAlert("Restart Aborted");
            }
            else
            {
                // Now we figure out what to set the timer to that does the notifications and calls, RestartNow()
                m_restartTimer.Interval = 15000;
                m_incrementsof15seconds = (int)seconds / 15;
                m_RestartTimerCounter = 0;
                m_restartTimer.AutoReset = true;
                m_restartTimer.Elapsed += new ElapsedEventHandler(RestartTimer_Elapsed);
                m_log.Error("[REGION]: Restarting Region in " + (seconds / 60) + " minutes");
                m_restartTimer.Start();
                SendRegionMessageFromEstateTools(UUID.Random(), UUID.Random(), String.Empty, RegionInfo.RegionName + ": Restarting in 2 Minutes");
                //SendGeneralAlert(RegionInfo.RegionName + ": Restarting in 2 Minutes");
            }
        }

        // The Restart timer has occured.
        // We have to figure out if this is a notification or if the number of seconds specified in Restart
        // have elapsed.
        // If they have elapsed, call RestartNow()
        public void RestartTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            m_RestartTimerCounter++;
            if (m_RestartTimerCounter <= m_incrementsof15seconds)
            {
                if (m_RestartTimerCounter == 4 || m_RestartTimerCounter == 6 || m_RestartTimerCounter == 7)
                    SendRegionMessageFromEstateTools(UUID.Random(), UUID.Random(), String.Empty, RegionInfo.RegionName + ": Restarting in " +
                                                     ((8 - m_RestartTimerCounter) * 15) + " seconds");

                // SendGeneralAlert(RegionInfo.RegionName + ": Restarting in " + ((8 - m_RestartTimerCounter)*15) +
                //" seconds");
            }
            else
            {
                m_restartTimer.Stop();
                m_restartTimer.AutoReset = false;
                RestartNow();
            }
        }

        // This causes the region to restart immediatley.
        public void RestartNow()
        {
            if (PhysicsScene != null)
            {
                PhysicsScene.Dispose();
            }

            m_log.Error("[REGION]: Closing");
            Close();
            m_log.Error("[REGION]: Firing Region Restart Message");
            base.Restart(0);
        }

        // This is a helper function that notifies root agents in this region that a new sim near them has come up
        // This is in the form of a timer because when an instance of OpenSim.exe is started,
        // Even though the sims initialize, they don't listen until 'all of the sims are initialized'
        // If we tell an agent about a sim that's not listening yet, the agent will not be able to connect to it.
        // subsequently the agent will never see the region come back online.
        public void RestartNotifyWaitElapsed(object sender, ElapsedEventArgs e)
        {
            m_restartWaitTimer.Stop();
            lock (m_regionRestartNotifyList)
            {
                foreach (RegionInfo region in m_regionRestartNotifyList)
                {
                    try
                    {
                        ForEachScenePresence(delegate(ScenePresence agent)
                                             {
                                                 // If agent is a root agent.
                                                 if (!agent.IsChildAgent)
                                                 {
                                                     //agent.ControllingClient.new
                                                     //this.CommsManager.InterRegion.InformRegionOfChildAgent(otherRegion.RegionHandle, agent.ControllingClient.RequestClientInfo());
                                                     InformClientOfNeighbor(agent, region);
                                                 }
                                             }
                            );
                    }
                    catch (NullReferenceException)
                    {
                        // This means that we're not booted up completely yet.
                        // This shouldn't happen too often anymore.
                    }
                }

                // Reset list to nothing.
                m_regionRestartNotifyList.Clear();
            }
        }

        public void SetSceneCoreDebug(bool ScriptEngine, bool CollisionEvents, bool PhysicsEngine)
        {
            if (m_scripts_enabled != !ScriptEngine)
            {
                // Tedd!   Here's the method to disable the scripting engine!
                if (ScriptEngine)
                {
                    m_log.Info("Stopping all Scripts in Scene");
                    lock (Entities)
                    {
                        foreach (EntityBase ent in Entities.Values)
                        {
                            if (ent is SceneObjectGroup)
                            {
                                ((SceneObjectGroup)ent).RemoveScriptInstances();
                            }
                        }
                    }
                }
                else
                {
                    m_log.Info("Starting all Scripts in Scene");
                    lock (Entities)
                    {
                        foreach (EntityBase ent in Entities.Values)
                        {
                            if (ent is SceneObjectGroup)
                            {
                                ((SceneObjectGroup)ent).CreateScriptInstances(0, false, DefaultScriptEngine, 0);
                            }
                        }
                    }
                }
                m_scripts_enabled = !ScriptEngine;
                m_log.Info("[TOTEDD]: Here is the method to trigger disabling of the scripting engine");
            }

            if (m_physics_enabled != !PhysicsEngine)
            {
                m_physics_enabled = !PhysicsEngine;
            }
        }

        public int GetInaccurateNeighborCount()
        {
            lock (m_neighbours)
            {
                return m_neighbours.Count;
            }
        }

        // This is the method that shuts down the scene.
        public override void Close()
        {
            m_log.InfoFormat("[SCENE]: Closing down the single simulator: {0}", RegionInfo.RegionName);

            // Kick all ROOT agents with the message, 'The simulator is going down'
            ForEachScenePresence(delegate(ScenePresence avatar)
                                 {
                                     if (avatar.KnownChildRegions.Contains(RegionInfo.RegionHandle))
                                         avatar.KnownChildRegions.Remove(RegionInfo.RegionHandle);

                                     if (!avatar.IsChildAgent)
                                         avatar.ControllingClient.Kick("The simulator is going down.");

                                     avatar.ControllingClient.SendShutdownConnectionNotice();
                                 });

            // Wait here, or the kick messages won't actually get to the agents before the scene terminates.
            Thread.Sleep(500);

            // Stop all client threads.
            ForEachScenePresence(delegate(ScenePresence avatar) { avatar.ControllingClient.Close(true); });
            
            // Stop updating the scene objects and agents.
            //m_heartbeatTimer.Close();
            shuttingdown = true;

            m_log.Debug("[SCENE]: Persisting changed objects");
            List<EntityBase> entities = GetEntities();
            foreach (EntityBase entity in entities)
            {
                if (!entity.IsDeleted && entity is SceneObjectGroup && ((SceneObjectGroup)entity).HasGroupChanged)
                {
                    ((SceneObjectGroup)entity).ProcessBackup(m_storageManager.DataStore);
                }
            }

            m_sceneGraph.Close();
            
            // De-register with region communications (events cleanup)
            UnRegisterRegionWithComms();

            // Shut down all non shared modules.
            foreach (IRegionModule module in Modules.Values)
            {
                if (!module.IsSharedModule)
                {
                    module.Close();
                }
            }
            Modules.Clear();

            // call the base class Close method.
            base.Close();
        }

        /// <summary>
        /// Start the timer which triggers regular scene updates
        /// </summary>
        public void StartTimer()
        {
            //m_log.Debug("[SCENE]: Starting timer");
            //m_heartbeatTimer.Enabled = true;
            //m_heartbeatTimer.Interval = (int)(m_timespan * 1000);
            //m_heartbeatTimer.Elapsed += new ElapsedEventHandler(Heartbeat);
            HeartbeatThread = new Thread(new ParameterizedThreadStart(Heartbeat));
            HeartbeatThread.SetApartmentState(ApartmentState.MTA);
            HeartbeatThread.Name = "Heartbeat";
            HeartbeatThread.Priority = ThreadPriority.AboveNormal;
            ThreadTracker.Add(HeartbeatThread);
            HeartbeatThread.Start();
        }

        /// <summary>
        /// Sets up references to modules required by the scene
        /// </summary>
        public void SetModuleInterfaces()
        {
            m_httpRequestModule = RequestModuleInterface<IHttpRequests>();
            m_xmlrpcModule = RequestModuleInterface<IXMLRPC>();
            m_worldCommModule = RequestModuleInterface<IWorldComm>();
            XferManager = RequestModuleInterface<IXfer>();
            m_AvatarFactory = RequestModuleInterface<IAvatarFactory>();
            m_archiver = RequestModuleInterface<IRegionArchiver>();
            m_serialiser = RequestModuleInterface<IRegionSerialiser>();
        }

        #endregion

        #region Update Methods

        /// <summary>
        /// Performs per-frame updates regularly
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Heartbeat(object sender)
        {
            Update();
        }

        /// <summary>
        /// Performs per-frame updates on the scene, this should be the central scene loop
        /// </summary>
        public override void Update()
        {
            int maintc = 0;
            while (!shuttingdown)
            {
                maintc = System.Environment.TickCount;

                TimeSpan SinceLastFrame = DateTime.Now - m_lastupdate;
                // Aquire a lock so only one update call happens at once
                //updateLock.WaitOne();
                float physicsFPS = 0;
                //m_log.Info("sadfadf" + m_neighbours.Count.ToString());
                int agentsInScene = m_sceneGraph.GetRootAgentCount() + m_sceneGraph.GetChildAgentCount();

                if (agentsInScene > 21)
                {
                    if (m_update_entities == 1)
                    {
                        m_update_entities = 5;
                        m_statsReporter.SetUpdateMS(6000);
                    }
                }
                else
                {
                    if (m_update_entities == 5)
                    {
                        m_update_entities = 1;
                        m_statsReporter.SetUpdateMS(3000);
                    }
                }

                frameMS = System.Environment.TickCount;
                try
                {
                    // Increment the frame counter
                    m_frame++;

                    // Loop it
                    if (m_frame == Int32.MaxValue)
                        m_frame = 0;

                    physicsMS2 = System.Environment.TickCount;
                    if ((m_frame % m_update_physics == 0) && m_physics_enabled)
                        m_sceneGraph.UpdatePreparePhysics();
                    physicsMS2 = System.Environment.TickCount - physicsMS2;

                    if (m_frame % m_update_entitymovement == 0)
                        m_sceneGraph.UpdateEntityMovement();

                    physicsMS = System.Environment.TickCount;
                    if ((m_frame % m_update_physics == 0) && m_physics_enabled)
                        physicsFPS = m_sceneGraph.UpdatePhysics(
                            Math.Max(SinceLastFrame.TotalSeconds, m_timespan)
                            );
                    if (m_frame % m_update_physics == 0 && SynchronizeScene != null)
                        SynchronizeScene(this);

                    physicsMS = System.Environment.TickCount - physicsMS;
                    physicsMS += physicsMS2;

                    otherMS = System.Environment.TickCount;
                    // run through all entities looking for updates (slow)
                    if (m_frame % m_update_entities == 0)
                        m_sceneGraph.UpdateEntities();

                    // run through entities that have scheduled themselves for
                    // updates looking for updates(faster)
                    if (m_frame % m_update_entitiesquick == 0)
                        m_sceneGraph.ProcessUpdates();

                    // Run through scenepresences looking for updates
                    if (m_frame % m_update_presences == 0)
                        m_sceneGraph.UpdatePresences();

                    // Delete temp-on-rez stuff
                    if (m_frame % m_update_backup == 0)
                        CleanTempObjects();

                    if (Region_Status != RegionStatus.SlaveScene)
                    {
                        if (m_frame % m_update_events == 0)
                            UpdateEvents();

                        if (m_frame % m_update_backup == 0)
                        {
                            UpdateStorageBackup();
                        }

                        if (m_frame % m_update_terrain == 0)
                            UpdateTerrain();

                        if (m_frame % m_update_land == 0)
                            UpdateLand();
                        otherMS = System.Environment.TickCount - otherMS;
                        // if (m_frame%m_update_avatars == 0)
                        //   UpdateInWorldTime();
                        m_statsReporter.AddPhysicsFPS(physicsFPS);
                        m_statsReporter.AddTimeDilation(m_timedilation);
                        m_statsReporter.AddFPS(1);
                        m_statsReporter.AddInPackets(0);
                        m_statsReporter.SetRootAgents(m_sceneGraph.GetRootAgentCount());
                        m_statsReporter.SetChildAgents(m_sceneGraph.GetChildAgentCount());
                        m_statsReporter.SetObjects(m_sceneGraph.GetTotalObjectsCount());
                        m_statsReporter.SetActiveObjects(m_sceneGraph.GetActiveObjectsCount());
                        frameMS = System.Environment.TickCount - frameMS;
                        m_statsReporter.addFrameMS(frameMS);
                        m_statsReporter.addPhysicsMS(physicsMS);
                        m_statsReporter.addOtherMS(otherMS);
                        m_statsReporter.SetActiveScripts(m_sceneGraph.GetActiveScriptsCount());
                        m_statsReporter.addScriptLines(m_sceneGraph.GetScriptLPS());
                    }
                }
                catch (NotImplementedException)
                {
                    throw;
                }
                catch (AccessViolationException e)
                {
                    m_log.Error("[Scene]: Failed with exception " + e.ToString() + " On Region: " + RegionInfo.RegionName);
                }
                catch (NullReferenceException e)
                {
                    m_log.Error("[Scene]: Failed with exception " + e.ToString() + " On Region: " + RegionInfo.RegionName);
                }
                catch (InvalidOperationException e)
                {
                    m_log.Error("[Scene]: Failed with exception " + e.ToString() + " On Region: " + RegionInfo.RegionName);
                }
                catch (Exception e)
                {
                    m_log.Error("[Scene]: Failed with exception " + e.ToString() + " On Region: " + RegionInfo.RegionName);
                }
                finally
                {
                    //updateLock.ReleaseMutex();
                    // Get actual time dilation
                    float tmpval = (m_timespan / (float)SinceLastFrame.TotalSeconds);

                    // If actual time dilation is greater then one, we're catching up, so subtract
                    // the amount that's greater then 1 from the time dilation
                    if (tmpval > 1.0)
                    {
                        tmpval = tmpval - (tmpval - 1.0f);
                    }
                    m_timedilation = tmpval;

                    m_lastupdate = DateTime.Now;
                }
                maintc = System.Environment.TickCount - maintc;
                maintc = (int)(m_timespan * 1000) - maintc;

                if ((maintc < (m_timespan * 1000)) && maintc > 0)
                    Thread.Sleep(maintc);
            }
        }

        private void SendSimStatsPackets(SimStats stats)
        {
            List<ScenePresence> StatSendAgents = GetScenePresences();
            foreach (ScenePresence agent in StatSendAgents)
            {
                if (!agent.IsChildAgent)
                {
                    agent.ControllingClient.SendSimStats(stats);
                }
            }
        }

        private void UpdateLand()
        {
            if (LandChannel != null)
            {
                if (LandChannel.IsLandPrimCountTainted())
                {
                    EventManager.TriggerParcelPrimCountUpdate();
                }
            }
        }

        private void UpdateTerrain()
        {
            EventManager.TriggerTerrainTick();
        }

        private void UpdateStorageBackup()
        {
            if (!m_backingup)
            {
                m_backingup = true;
                Thread backupthread = new Thread(Backup);
                backupthread.Name = "BackupWriter";
                backupthread.IsBackground = true;
                backupthread.Start();
            }
        }

        private void UpdateEvents()
        {
            m_eventManager.TriggerOnFrame();
        }

        /// <summary>
        /// Perform delegate action on all clients subscribing to updates from this region.
        /// </summary>
        /// <returns></returns>
        internal void Broadcast(Action<IClientAPI> whatToDo)
        {
            ForEachScenePresence(delegate(ScenePresence presence) { whatToDo(presence.ControllingClient); });
        }

        /// <summary>
        /// Backup the scene.  This acts as the main method of the backup thread.
        /// </summary>
        /// <returns></returns>
        public void Backup()
        {
            lock (m_returns)
            {
                EventManager.TriggerOnBackup(m_storageManager.DataStore);
                m_backingup = false;

                foreach (KeyValuePair<UUID, ReturnInfo> ret in m_returns)
                {
                    UUID transaction = UUID.Random();

                    GridInstantMessage msg = new GridInstantMessage();
                    msg.fromAgentID = new Guid(UUID.Zero.ToString()); // From server
                    msg.toAgentID = new Guid(ret.Key.ToString());
                    msg.imSessionID = new Guid(transaction.ToString());
                    msg.timestamp = (uint)Util.UnixTimeSinceEpoch();
                    msg.fromAgentName = "Server";
                    msg.dialog = (byte)19; // Object msg
                    msg.fromGroup = false;
                    msg.offline = (byte)1;
                    msg.ParentEstateID = RegionInfo.EstateSettings.ParentEstateID;
                    msg.Position = Vector3.Zero;
                    msg.RegionID = RegionInfo.RegionID.Guid;
                    msg.binaryBucket = new byte[0];
                    if (ret.Value.count > 1)
                        msg.message = string.Format("Your {0} objects were returned from {1} in region {2} due to {3}", ret.Value.count, ret.Value.location.ToString(), RegionInfo.RegionName, ret.Value.reason);
                    else
                        msg.message = string.Format("Your object {0} was returned from {1} in region {2} due to {3}", ret.Value.objectName, ret.Value.location.ToString(), RegionInfo.RegionName, ret.Value.reason);

                    IMessageTransferModule tr = RequestModuleInterface<IMessageTransferModule>();
                    if (tr != null)
                        tr.SendInstantMessage(msg, delegate(bool success) {} );
                }
                m_returns.Clear();
            }
        }

        public void AddReturn(UUID agentID, string objectName, Vector3 location, string reason)
        {
            lock (m_returns)
            {
                if (m_returns.ContainsKey(agentID))
                {
                    ReturnInfo info = m_returns[agentID];
                    info.count++;
                    m_returns[agentID] = info;
                }
                else
                {
                    ReturnInfo info = new ReturnInfo();
                    info.count = 1;
                    info.objectName = objectName;
                    info.location = location;
                    info.reason = reason;
                    m_returns[agentID] = info;
                }
            }
        }

        #endregion

        #region Load Terrain

        public void ExportWorldMap(string fileName)
        {
            List<MapBlockData> mapBlocks =
                m_sceneGridService.RequestNeighbourMapBlocks((int)(RegionInfo.RegionLocX - 9),
                                                             (int)(RegionInfo.RegionLocY - 9),
                                                             (int)(RegionInfo.RegionLocX + 9),
                                                             (int)(RegionInfo.RegionLocY + 9));
            List<AssetBase> textures = new List<AssetBase>();
            List<Image> bitImages = new List<Image>();

            foreach (MapBlockData mapBlock in mapBlocks)
            {
                AssetBase texAsset = AssetCache.GetAsset(mapBlock.MapImageId, true);

                if (texAsset != null)
                {
                    textures.Add(texAsset);
                }
                else
                {
                    texAsset = AssetCache.GetAsset(mapBlock.MapImageId, true);
                    if (texAsset != null)
                    {
                        textures.Add(texAsset);
                    }
                }
            }

            foreach (AssetBase asset in textures)
            {
                ManagedImage managedImage;
                Image image;

                if (OpenJPEG.DecodeToImage(asset.Data, out managedImage, out image))
                    bitImages.Add(image);
            }

            Bitmap mapTexture = new Bitmap(2560, 2560);
            Graphics g = Graphics.FromImage(mapTexture);
            SolidBrush sea = new SolidBrush(Color.DarkBlue);
            g.FillRectangle(sea, 0, 0, 2560, 2560);

            for (int i = 0; i < mapBlocks.Count; i++)
            {
                ushort x = (ushort)((mapBlocks[i].X - RegionInfo.RegionLocX) + 10);
                ushort y = (ushort)((mapBlocks[i].Y - RegionInfo.RegionLocY) + 10);
                g.DrawImage(bitImages[i], (x * 128), (y * 128), 128, 128);
            }
            mapTexture.Save(fileName, ImageFormat.Jpeg);
        }

        public void SaveTerrain()
        {
            m_storageManager.DataStore.StoreTerrain(Heightmap.GetDoubles(), RegionInfo.RegionID);
        }

        /// <summary>
        /// Loads the World heightmap
        /// </summary>
        ///
        public override void LoadWorldMap()
        {
            try
            {
                double[,] map = m_storageManager.DataStore.LoadTerrain(RegionInfo.RegionID);
                if (map == null)
                {
                    m_log.Info("[TERRAIN]: No default terrain. Generating a new terrain.");
                    Heightmap = new TerrainChannel();

                    m_storageManager.DataStore.StoreTerrain(Heightmap.GetDoubles(), RegionInfo.RegionID);
                }
                else
                {
                    Heightmap = new TerrainChannel(map);
                }

            }
            catch (Exception e)
            {
                m_log.Warn("[terrain]: Scene.cs: LoadWorldMap() - Failed with exception " + e.ToString());
            }
        }

        /// <summary>
        /// Register this region with a grid service
        /// </summary>
        /// <exception cref="System.Exception">Thrown if registration of the region itself fails.</exception>
        public void RegisterRegionWithGrid()
        {
            RegisterCommsEvents();

            // These two 'commands' *must be* next to each other or sim rebooting fails.
            m_sceneGridService.RegisterRegion(RegionInfo);
            m_sceneGridService.InformNeighborsThatRegionisUp(RegionInfo);

            Dictionary<string, string> dGridSettings = m_sceneGridService.GetGridSettings();

            if (dGridSettings.ContainsKey("allow_forceful_banlines"))
            {
                if (dGridSettings["allow_forceful_banlines"] != "TRUE")
                {
                    m_log.Info("[GRID]: Grid is disabling forceful parcel banlists");
                    EventManager.TriggerSetAllowForcefulBan(false);
                }
                else
                {
                    m_log.Info("[GRID]: Grid is allowing forceful parcel banlists");
                    EventManager.TriggerSetAllowForcefulBan(true);
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        public void CreateTerrainTexture(bool temporary)
        {
            //create a texture asset of the terrain
            IMapImageGenerator terrain = RequestModuleInterface<IMapImageGenerator>();

            // Cannot create a map for a nonexistant heightmap yet.
            if (Heightmap == null)
                return;

            if (terrain == null)
            {
                #region Fallback default maptile generation

                int tc = System.Environment.TickCount;
                m_log.Info("[MAPTILE]: Generating Maptile Step 1: Terrain");
                Bitmap mapbmp = new Bitmap(256, 256);
                double[,] hm = Heightmap.GetDoubles();
                bool ShadowDebugContinue = true;
                //Color prim = Color.FromArgb(120, 120, 120);
                //Vector3 RayEnd = new Vector3(0, 0, 0);
                //Vector3 RayStart = new Vector3(0, 0, 0);
                //Vector3 direction = new Vector3(0, 0, -1);
                //Vector3 AXOrigin = new Vector3();
                //Vector3 AXdirection = new Vector3();
                //Ray testRay = new Ray();
                //EntityIntersection rt = new EntityIntersection();
                bool terraincorruptedwarningsaid = false;

                float low = 255;
                float high = 0;
                for (int x = 0; x < 256; x++)
                {
                    for (int y = 0; y < 256; y++)
                    {
                        float hmval = (float)hm[x, y];
                        if (hmval < low)
                            low = hmval;
                        if (hmval > high)
                            high = hmval;
                    }
                }

                float mid = (high + low) * 0.5f;

                // temporary initializer
                float hfvalue = (float)m_regInfo.RegionSettings.WaterHeight;
                float hfvaluecompare = hfvalue;
                float hfdiff = hfvalue;
                int hfdiffi = 0;

                for (int x = 0; x < 256; x++)
                {
                    //int tc = System.Environment.TickCount;
                    for (int y = 0; y < 256; y++)
                    {
                        //RayEnd = new Vector3(x, y, 0);
                        //RayStart = new Vector3(x, y, 255);

                        //direction = Vector3.Norm(RayEnd - RayStart);
                        //AXOrigin = new Vector3(RayStart.X, RayStart.Y, RayStart.Z);
                        //AXdirection = new Vector3(direction.X, direction.Y, direction.Z);

                        //testRay = new Ray(AXOrigin, AXdirection);
                        //rt = m_sceneGraph.GetClosestIntersectingPrim(testRay);

                        //if (rt.HitTF)
                        //{
                        //mapbmp.SetPixel(x, y, prim);
                        //}
                        //else
                        //{
                        //float tmpval = (float)hm[x, y];
                        float heightvalue = (float)hm[x, y];

                        if (heightvalue > (float)m_regInfo.RegionSettings.WaterHeight)
                        {
                            // scale height value
                            heightvalue = low + mid * (heightvalue - low) / mid;

                            if (heightvalue > 255)
                                heightvalue = 255;

                            if (heightvalue < 0)
                                heightvalue = 0;

                            if (Single.IsInfinity(heightvalue) || Single.IsNaN(heightvalue))
                                heightvalue = 0;

                            try
                            {
                                Color green = Color.FromArgb((int)heightvalue, 100, (int)heightvalue);

                                // Y flip the cordinates
                                mapbmp.SetPixel(x, (256 - y) - 1, green);

                                //X
                                // .
                                //
                                // Shade the terrain for shadows
                                if ((x - 1 > 0) && (y - 1 > 0))
                                {
                                    hfvalue = (float)hm[x, y];
                                    hfvaluecompare = (float)hm[x - 1, y - 1];

                                    if (Single.IsInfinity(hfvalue) || Single.IsNaN(hfvalue))
                                        hfvalue = 0;

                                    if (Single.IsInfinity(hfvaluecompare) || Single.IsNaN(hfvaluecompare))
                                        hfvaluecompare = 0;

                                    hfdiff = hfvaluecompare - hfvalue;

                                    if (hfdiff > 0.3f)
                                    {

                                    }
                                    else if (hfdiff < -0.3f)
                                    {
                                        // We have to desaturate and blacken the land at the same time
                                        // we use floats, colors use bytes, so shrink are space down to
                                        // 0-255

                                        try
                                        {
                                            hfdiffi = Math.Abs((int)((hfdiff * 4) + (hfdiff * 0.5))) + 1;
                                            if (hfdiff % 1 != 0)
                                            {
                                                hfdiffi = hfdiffi + Math.Abs((int)(((hfdiff % 1) * 0.5f) * 10f) - 1);
                                            }
                                        }
                                        catch (System.OverflowException)
                                        {
                                            m_log.Debug("[MAPTILE]: Shadow failed at value: " + hfdiff.ToString());
                                            ShadowDebugContinue = false;
                                        }

                                        if (ShadowDebugContinue)
                                        {
                                            if ((256 - y) - 1 > 0)
                                            {
                                                Color Shade = mapbmp.GetPixel(x - 1, (256 - y) - 1);

                                                int r = Shade.R;

                                                int g = Shade.G;
                                                int b = Shade.B;
                                                Shade = Color.FromArgb((r - hfdiffi > 0) ? r - hfdiffi : 0, (g - hfdiffi > 0) ? g - hfdiffi : 0, (b - hfdiffi > 0) ? b - hfdiffi : 0);
                                                //Console.WriteLine("d:" + hfdiff.ToString() + ", i:" + hfdiffi + ", pos: " + x + "," + y + " - R:" + Shade.R.ToString() + ", G:" + Shade.G.ToString() + ", B:" + Shade.G.ToString());
                                                mapbmp.SetPixel(x - 1, (256 - y) - 1, Shade);
                                            }
                                        }
                                    }
                                }
                            }
                            catch (System.ArgumentException)
                            {
                                if (!terraincorruptedwarningsaid)
                                {
                                    m_log.WarnFormat("[MAPIMAGE]: Your terrain is corrupted in region {0}, it might take a few minutes to generate the map image depending on the corruption level", RegionInfo.RegionName);
                                    terraincorruptedwarningsaid = true;
                                }
                                Color black = Color.Black;
                                mapbmp.SetPixel(x, (256 - y) - 1, black);
                            }
                        }
                        else
                        {
                            // Y flip the cordinates
                            heightvalue = (float)m_regInfo.RegionSettings.WaterHeight - heightvalue;
                            if (heightvalue > 19)
                                heightvalue = 19;
                            if (heightvalue < 0)
                                heightvalue = 0;

                            heightvalue = 100 - (heightvalue * 100) / 19;

                            if (heightvalue > 255)
                                heightvalue = 255;

                            if (heightvalue < 0)
                                heightvalue = 0;

                            if (Single.IsInfinity(heightvalue) || Single.IsNaN(heightvalue))
                                heightvalue = 0;

                            try
                            {
                                Color water = Color.FromArgb((int)heightvalue, (int)heightvalue, 255);
                                mapbmp.SetPixel(x, (256 - y) - 1, water);
                            }
                            catch (System.ArgumentException)
                            {
                                if (!terraincorruptedwarningsaid)
                                {
                                    m_log.WarnFormat("[MAPIMAGE]: Your terrain is corrupted in region {0}, it might take a few minutes to generate the map image depending on the corruption level", RegionInfo.RegionName);
                                    terraincorruptedwarningsaid = true;
                                }
                                Color black = Color.Black;
                                mapbmp.SetPixel(x, (256 - y) - 1, black);
                            }
                        }
                    }
                    //}

                    //tc = System.Environment.TickCount - tc;
                    //m_log.Info("[MAPTILE]: Completed One row in " + tc + " ms");
                }
                
                m_log.Info("[MAPTILE]: Generating Maptile Step 1: Done in " + (System.Environment.TickCount - tc) + " ms");

                bool drawPrimVolume = true;

                try
                {
                    IConfig startupConfig = m_config.Configs["Startup"];
                    drawPrimVolume = startupConfig.GetBoolean("DrawPrimOnMapTile", true);
                }
                catch
                {
                    m_log.Warn("[MAPTILE]: Failed to load StartupConfig");
                }

                if (drawPrimVolume)
                {
                    tc = System.Environment.TickCount;
                    m_log.Info("[MAPTILE]: Generating Maptile Step 2: Object Volume Profile");
                    List<EntityBase> objs = GetEntities();

                    lock (objs)
                    {
                        foreach (EntityBase obj in objs)
                        {
                            // Only draw the contents of SceneObjectGroup
                            if (obj is SceneObjectGroup)
                            {
                                SceneObjectGroup mapdot = (SceneObjectGroup)obj;
                                Color mapdotspot = Color.Gray; // Default color when prim color is white
                                // Loop over prim in group
                                foreach (SceneObjectPart part in mapdot.Children.Values)
                                {
                                    if (part == null)
                                        continue;

                                    // Draw if the object is at least 1 meter wide in any direction
                                    if (part.Scale.X > 1f || part.Scale.Y > 1f || part.Scale.Z > 1f)
                                    {
                                        // Try to get the RGBA of the default texture entry..
                                        //
                                        try
                                        {
                                            if (part == null)
                                                continue;

                                            if (part.Shape == null)
                                                continue;

                                            if (part.Shape.PCode == (byte)PCode.Tree || part.Shape.PCode == (byte)PCode.NewTree)
                                                continue; // eliminates trees from this since we don't really have a good tree representation
                                            // if you want tree blocks on the map comment the above line and uncomment the below line
                                            //mapdotspot = Color.PaleGreen;

                                            if (part.Shape.Textures == null)
                                                continue;

                                            if (part.Shape.Textures.DefaultTexture == null)
                                                continue;

                                            Color4 texcolor = part.Shape.Textures.DefaultTexture.RGBA;

                                            // Not sure why some of these are null, oh well.

                                            int colorr = 255 - (int)(texcolor.R * 255f);
                                            int colorg = 255 - (int)(texcolor.G * 255f);
                                            int colorb = 255 - (int)(texcolor.B * 255f);

                                            if (!(colorr == 255 && colorg == 255 && colorb == 255))
                                            {
                                                //Try to set the map spot color
                                                try
                                                {
                                                    // If the color gets goofy somehow, skip it *shakes fist at Color4
                                                    mapdotspot = Color.FromArgb(colorr, colorg, colorb);
                                                }
                                                catch (ArgumentException)
                                                {
                                                }
                                            }
                                        }
                                        catch (IndexOutOfRangeException)
                                        {
                                            // Windows Array
                                        }
                                        catch (ArgumentOutOfRangeException)
                                        {
                                            // Mono Array
                                        }

                                        Vector3 pos = part.GetWorldPosition();

                                        // skip prim outside of retion
                                        if (pos.X < 0f || pos.X > 256f || pos.Y < 0f || pos.Y > 256f)
                                            continue;

                                        // skip prim in non-finite position
                                        if (Single.IsNaN(pos.X) || Single.IsNaN(pos.Y) || Single.IsInfinity(pos.X)
                                                                || Single.IsInfinity(pos.Y))
                                            continue;

                                        // Figure out if object is under 256m above the height of the terrain
                                        bool isBelow256AboveTerrain = false;

                                        try
                                        {
                                            isBelow256AboveTerrain = (pos.Z < ((float)hm[(int)pos.X, (int)pos.Y] + 256f));
                                        }
                                        catch (Exception)
                                        {
                                        }

                                        if (isBelow256AboveTerrain)
                                        {
                                            // Translate scale by rotation so scale is represented properly when object is rotated
                                            Vector3 scale = part.Shape.Scale;
                                            Quaternion rot = part.GetWorldRotation();
                                            scale *= rot;

                                            // negative scales don't work in this situation
                                            scale.X = Math.Abs(scale.X);
                                            scale.Y = Math.Abs(scale.Y);
                                            scale.Z = Math.Abs(scale.Z);

                                            // This scaling isn't very accurate and doesn't take into account the face rotation :P
                                            int mapdrawstartX = (int)(pos.X - scale.X);
                                            int mapdrawstartY = (int)(pos.Y - scale.Y);
                                            int mapdrawendX = (int)(pos.X + scale.X);
                                            int mapdrawendY = (int)(pos.Y + scale.Y);

                                            // If object is beyond the edge of the map, don't draw it to avoid errors
                                            if (mapdrawstartX < 0 || mapdrawstartX > 255 || mapdrawendX < 0 || mapdrawendX > 255
                                                                  || mapdrawstartY < 0 || mapdrawstartY > 255 || mapdrawendY < 0
                                                                  || mapdrawendY > 255)
                                                continue;

                                            int wy = 0;

                                            bool breakYN = false; // If we run into an error drawing, break out of the
                                            // loop so we don't lag to death on error handling
                                            for (int wx = mapdrawstartX; wx < mapdrawendX; wx++)
                                            {
                                                for (wy = mapdrawstartY; wy < mapdrawendY; wy++)
                                                {
                                                    //m_log.InfoFormat("[MAPDEBUG]: {0},{1}({2})", wx, (255 - wy),wy);
                                                    try
                                                    {
                                                        // Remember, flip the y!
                                                        mapbmp.SetPixel(wx, (255 - wy), mapdotspot);
                                                    }
                                                    catch (ArgumentException)
                                                    {
                                                        breakYN = true;
                                                    }

                                                    if (breakYN)
                                                        break;
                                                }

                                                if (breakYN)
                                                    break;
                                            }
                                        } // Object is within 256m Z of terrain
                                    } // object is at least a meter wide
                                } // loop over group children
                            } // entitybase is sceneobject group
                        } // foreach loop over entities
                    } // lock entities objs

                    m_log.Info("[MAPTILE]: Generating Maptile Step 2: Done in " + (System.Environment.TickCount - tc) + " ms");
                } // end if drawPrimOnMaptle

                byte[] data;
                try
                {
                    data = OpenJPEG.EncodeFromImage(mapbmp, false);
                }
                catch (Exception)
                {
                    return;
                }

                LazySaveGeneratedMaptile(data,temporary);

                #endregion
            }
            else
            {
                // Use the module to generate the maptile.
                byte[] data = terrain.WriteJpeg2000Image("defaultstripe.png");
                if (data != null)
                {
                    LazySaveGeneratedMaptile(data,temporary);
                }
            }
        }

        public void LazySaveGeneratedMaptile(byte[] data, bool temporary)
        {
            // Overwrites the local Asset cache with new maptile data
            // Assets are single write, this causes the asset server to ignore this update,
            // but the local asset cache does not

            // this is on purpose!  The net result of this is the region always has the most up to date
            // map tile while protecting the (grid) asset database from bloat caused by a new asset each
            // time a mapimage is generated!

            UUID lastMapRegionUUID = m_regInfo.lastMapUUID;

            int lastMapRefresh = 0;
            int twoDays = 172800;
            int RefreshSeconds = twoDays;

            try
            {
                lastMapRefresh = Convert.ToInt32(m_regInfo.lastMapRefresh);
            }
            catch (ArgumentException)
            {
            }
            catch (FormatException)
            {
            }
            catch (OverflowException)
            {
            }

            UUID TerrainImageUUID = UUID.Random();

            if (lastMapRegionUUID == UUID.Zero || (lastMapRefresh + RefreshSeconds) < Util.UnixTimeSinceEpoch())
            {
                m_regInfo.SaveLastMapUUID(TerrainImageUUID);

                m_log.Warn("[MAPTILE]: STORING MAPTILE IMAGE");
            }
            else
            {
                TerrainImageUUID = lastMapRegionUUID;
                m_log.Warn("[MAPTILE]: REUSING OLD MAPTILE IMAGE ID");
            }

            m_regInfo.RegionSettings.TerrainImageID = TerrainImageUUID;

            AssetBase asset = new AssetBase();
            asset.FullID = m_regInfo.RegionSettings.TerrainImageID;
            asset.Data = data;
            asset.Name = "terrainImage_" + m_regInfo.RegionID.ToString() + "_" + lastMapRefresh.ToString();
            asset.Description = RegionInfo.RegionName;

            asset.Type = 0;
            asset.Temporary = temporary;
            AssetCache.AddAsset(asset);
        }

        #endregion

        #region Load Land

        public void loadAllLandObjectsFromStorage(UUID regionID)
        {
            m_log.Info("[SCENE]: Loading land objects from storage");
            List<LandData> landData = m_storageManager.DataStore.LoadLandObjects(regionID);

            if (LandChannel != null)
            {
                if (landData.Count == 0)
                {
                    EventManager.TriggerNoticeNoLandDataFromStorage();
                }
                else
                {
                    EventManager.TriggerIncomingLandDataFromStorage(landData);
                }
            }
            else
            {
                m_log.Error("[SCENE]: Land Channel is not defined. Cannot load from storage!");
            }
        }

        #endregion

        #region Primitives Methods

        /// <summary>
        /// Loads the World's objects
        /// </summary>
        public virtual void LoadPrimsFromStorage(UUID regionID)
        {
            m_log.Info("[SCENE]: Loading objects from datastore");

            List<SceneObjectGroup> PrimsFromDB = m_storageManager.DataStore.LoadObjects(regionID);
            foreach (SceneObjectGroup group in PrimsFromDB)
            {
                if (group.RootPart == null)
                {
                    m_log.ErrorFormat("[SCENE] Found a SceneObjectGroup with m_rootPart == null and {0} children",
                                      group.Children == null ? 0 : group.Children.Count);
                }
                
                AddRestoredSceneObject(group, true, true);
                SceneObjectPart rootPart = group.GetChildPart(group.UUID);
                rootPart.ObjectFlags &= ~(uint)PrimFlags.Scripted;
                rootPart.TrimPermissions();
                group.CheckSculptAndLoad();
                //rootPart.DoPhysicsPropertyUpdate(UsePhysics, true);
            }

            m_log.Info("[SCENE]: Loaded " + PrimsFromDB.Count.ToString() + " SceneObject(s)");
        }

        public Vector3 GetNewRezLocation(Vector3 RayStart, Vector3 RayEnd, UUID RayTargetID, Quaternion rot, byte bypassRayCast, byte RayEndIsIntersection, bool frontFacesOnly, Vector3 scale, bool FaceCenter)
        {
            Vector3 pos = Vector3.Zero;
            if (RayEndIsIntersection == (byte)1)
            {
                pos = RayEnd;
                return pos;
            }

            if (RayTargetID != UUID.Zero)
            {
                SceneObjectPart target = GetSceneObjectPart(RayTargetID);

                Vector3 direction = Vector3.Normalize(RayEnd - RayStart);
                Vector3 AXOrigin = new Vector3(RayStart.X, RayStart.Y, RayStart.Z);
                Vector3 AXdirection = new Vector3(direction.X, direction.Y, direction.Z);

                if (target != null)
                {
                    pos = target.AbsolutePosition;
                    //m_log.Info("[OBJECT_REZ]: TargetPos: " + pos.ToString() + ", RayStart: " + RayStart.ToString() + ", RayEnd: " + RayEnd.ToString() + ", Volume: " + Util.GetDistanceTo(RayStart,RayEnd).ToString() + ", mag1: " + Util.GetMagnitude(RayStart).ToString() + ", mag2: " + Util.GetMagnitude(RayEnd).ToString());

                    // TODO: Raytrace better here

                    //EntityIntersection ei = m_sceneGraph.GetClosestIntersectingPrim(new Ray(AXOrigin, AXdirection));
                    Ray NewRay = new Ray(AXOrigin, AXdirection);

                    // Ray Trace against target here
                    EntityIntersection ei = target.TestIntersectionOBB(NewRay, Quaternion.Identity, frontFacesOnly, FaceCenter);

                    // Un-comment out the following line to Get Raytrace results printed to the console.
                   // m_log.Info("[RAYTRACERESULTS]: Hit:" + ei.HitTF.ToString() + " Point: " + ei.ipoint.ToString() + " Normal: " + ei.normal.ToString());
                    float ScaleOffset = 0.5f;

                    // If we hit something
                    if (ei.HitTF)
                    {
                        Vector3 scaleComponent = new Vector3(ei.AAfaceNormal.X, ei.AAfaceNormal.Y, ei.AAfaceNormal.Z);
                        if (scaleComponent.X != 0) ScaleOffset = scale.X;
                        if (scaleComponent.Y != 0) ScaleOffset = scale.Y;
                        if (scaleComponent.Z != 0) ScaleOffset = scale.Z;
                        ScaleOffset = Math.Abs(ScaleOffset);
                        Vector3 intersectionpoint = new Vector3(ei.ipoint.X, ei.ipoint.Y, ei.ipoint.Z);
                        Vector3 normal = new Vector3(ei.normal.X, ei.normal.Y, ei.normal.Z);
                        // Set the position to the intersection point
                        Vector3 offset = (normal * (ScaleOffset / 2f));
                        pos = (intersectionpoint + offset);

                        // Un-offset the prim (it gets offset later by the consumer method)
                        pos.Z -= 0.25F;
                    }

                    return pos;
                }
                else
                {
                    // We don't have a target here, so we're going to raytrace all the objects in the scene.

                    EntityIntersection ei = m_sceneGraph.GetClosestIntersectingPrim(new Ray(AXOrigin, AXdirection), true, false);

                    // Un-comment the following line to print the raytrace results to the console.
                    //m_log.Info("[RAYTRACERESULTS]: Hit:" + ei.HitTF.ToString() + " Point: " + ei.ipoint.ToString() + " Normal: " + ei.normal.ToString());

                    if (ei.HitTF)
                    {
                        pos = new Vector3(ei.ipoint.X, ei.ipoint.Y, ei.ipoint.Z);
                    } else
                    {
                        // fall back to our stupid functionality
                        pos = RayEnd;
                    }

                    return pos;
                }
            }
            else
            {
                // fall back to our stupid functionality
                pos = RayEnd;
                return pos;
            }
        }

        public virtual void AddNewPrim(UUID ownerID, UUID groupID, Vector3 RayEnd, Quaternion rot, PrimitiveBaseShape shape,
                                       byte bypassRaycast, Vector3 RayStart, UUID RayTargetID,
                                       byte RayEndIsIntersection)
        {
            Vector3 pos = GetNewRezLocation(RayStart, RayEnd, RayTargetID, rot, bypassRaycast, RayEndIsIntersection, true, new Vector3(0.5f, 0.5f, 0.5f), false);

            if (ExternalChecks.ExternalChecksCanRezObject(1, ownerID, pos))
            {
                // rez ON the ground, not IN the ground
                pos.Z += 0.25F;

                AddNewPrim(ownerID, groupID, pos, rot, shape);
            }
        }

        public virtual SceneObjectGroup AddNewPrim(UUID ownerID, UUID groupID, Vector3 pos, Quaternion rot, PrimitiveBaseShape shape)
        {
            //m_log.DebugFormat(
            //    "[SCENE]: Scene.AddNewPrim() called for agent {0} in {1}", ownerID, RegionInfo.RegionName);

            SceneObjectGroup sceneObject = new SceneObjectGroup(ownerID, pos, rot, shape);

            SceneObjectPart rootPart = sceneObject.GetChildPart(sceneObject.UUID);
            // if grass or tree, make phantom
            //rootPart.TrimPermissions();
            if ((rootPart.Shape.PCode == (byte)PCode.Grass) 
                || (rootPart.Shape.PCode == (byte)PCode.Tree) || (rootPart.Shape.PCode == (byte)PCode.NewTree))
            {
                rootPart.AddFlag(PrimFlags.Phantom);
                //rootPart.ObjectFlags += (uint)PrimFlags.Phantom;
                if (rootPart.Shape.PCode != (byte)PCode.Grass)
                    AdaptTree(ref shape);
            }

            AddNewSceneObject(sceneObject, true);
            sceneObject.SetGroup(groupID, null);

            return sceneObject;
        }

        void AdaptTree(ref PrimitiveBaseShape tree)
        {
            // Tree size has to be adapted depending on its type
            switch ((Tree)tree.State)
            {
                case Tree.Cypress1:
                case Tree.Cypress2:
                    tree.Scale = new Vector3(4, 4, 10);
                    break;

                // case... other tree types
                // tree.Scale = new Vector3(?, ?, ?);
                // break;

                default:
                    tree.Scale = new Vector3(4, 4, 4);
                    break;
            }
        }

        public SceneObjectGroup AddTree(UUID uuid, UUID groupID, Vector3 scale, Quaternion rotation, Vector3 position,
                                        Tree treeType, bool newTree)
        {
            PrimitiveBaseShape treeShape = new PrimitiveBaseShape();
            treeShape.PathCurve = 16;
            treeShape.PathEnd = 49900;
            treeShape.PCode = newTree ? (byte)PCode.NewTree : (byte)PCode.Tree;
            treeShape.Scale = scale;
            treeShape.State = (byte)treeType;
            return AddNewPrim(uuid, groupID, position, rotation, treeShape);
        }

        /// <summary>
        /// Add an object into the scene that has come from storage
        /// </summary>
        /// <param name="sceneObject"></param>
        /// <param name="attachToBackup">
        /// If true, changes to the object will be reflected in its persisted data
        /// If false, the persisted data will not be changed even if the object in the scene is changed
        /// </param>
        /// <param name="alreadyPersisted">
        /// If true, we won't persist this object until it changes
        /// If false, we'll persist this object immediately
        /// </param>
        /// <returns>
        /// true if the object was added, false if an object with the same uuid was already in the scene
        /// </returns>
        public bool AddRestoredSceneObject(
            SceneObjectGroup sceneObject, bool attachToBackup, bool alreadyPersisted)
        {
            return m_sceneGraph.AddRestoredSceneObject(sceneObject, attachToBackup, alreadyPersisted);
        }

        /// <summary>
        /// Add a newly created object to the scene
        /// </summary>
        /// <param name="sceneObject"></param>
        /// <param name="attachToBackup">
        /// If true, the object is made persistent into the scene.
        /// If false, the object will not persist over server restarts
        /// </param>
        public bool AddNewSceneObject(SceneObjectGroup sceneObject, bool attachToBackup)
        {
            return m_sceneGraph.AddNewSceneObject(sceneObject, attachToBackup);
        }

        /// <summary>
        /// Delete every object from the scene
        /// </summary>
        public void DeleteAllSceneObjects()
        {
            lock (Entities)
            {
                ICollection<EntityBase> entities = new List<EntityBase>(Entities.Values);

                foreach (EntityBase e in entities)
                {
                    if (e is SceneObjectGroup)
                        DeleteSceneObject((SceneObjectGroup)e, false);
                }
            }
        }

        /// <summary>
        /// Synchronously delete the given object from the scene.
        /// </summary>
        /// <param name="group"></param>
        public void DeleteSceneObject(SceneObjectGroup group, bool silent)
        {
            //SceneObjectPart rootPart = group.GetChildPart(group.UUID);

            // Serialise calls to RemoveScriptInstances to avoid
            // deadlocking on m_parts inside SceneObjectGroup
            lock (m_deleting_scene_object)
            {
                group.RemoveScriptInstances();
            }

            foreach (SceneObjectPart part in group.Children.Values)
            {
                if (part.PhysActor != null)
                {
                    PhysicsScene.RemovePrim(part.PhysActor);
                    part.PhysActor = null;
                }
            }
//            if (rootPart.PhysActor != null)
//            {
//                PhysicsScene.RemovePrim(rootPart.PhysActor);
//                rootPart.PhysActor = null;
//            }

            if (UnlinkSceneObject(group.UUID, false))
            {
                EventManager.TriggerObjectBeingRemovedFromScene(group);
                EventManager.TriggerParcelPrimCountTainted();
            }

            group.DeleteGroup(silent);
        }

        /// <summary>
        /// Unlink the given object from the scene.  Unlike delete, this just removes the record of the object - the
        /// object itself is not destroyed.
        /// </summary>
        /// <param name="uuid"></param>
        /// <returns>true if the object was in the scene, false if it was not</returns>
        public bool UnlinkSceneObject(UUID uuid, bool resultOfLinkingObjects)
        {
            if (m_sceneGraph.DeleteSceneObject(uuid, resultOfLinkingObjects))
            {
                if (!resultOfLinkingObjects)
                    m_storageManager.DataStore.RemoveObject(uuid,
                            m_regInfo.RegionID);
                return true;
            }

            return false;
        }

        public void LoadPrimsFromXml(string fileName, bool newIdsFlag, Vector3 loadOffset)
        {
            m_log.InfoFormat("[SCENE]: Loading prims in xml format to region {0} from {1}", RegionInfo.RegionName, fileName);

            m_serialiser.LoadPrimsFromXml(this, fileName, newIdsFlag, loadOffset);
        }

        public void SavePrimsToXml(string fileName)
        {
            m_log.InfoFormat("[SCENE]: Saving prims in xml format for region {0} to {1}", RegionInfo.RegionName, fileName);

            m_serialiser.SavePrimsToXml(this, fileName);
        }

        public void LoadPrimsFromXml2(string fileName)
        {
            m_log.InfoFormat("[SCENE]: Loading prims in xml2 format to region {0} from {1}", RegionInfo.RegionName, fileName);

            m_serialiser.LoadPrimsFromXml2(this, fileName);
        }

        public void LoadPrimsFromXml2(TextReader reader, bool startScripts)
        {
            m_log.InfoFormat("[SCENE]: Loading prims in xml2 format to region {0} from stream", RegionInfo.RegionName);

            m_serialiser.LoadPrimsFromXml2(this, reader, startScripts);
        }

        public void SavePrimsToXml2(string fileName)
        {
            m_log.InfoFormat("[SCENE]: Saving prims in xml2 format for region {0} to {1}", RegionInfo.RegionName, fileName);

            m_serialiser.SavePrimsToXml2(this, fileName);
        }

        public void SavePrimsToXml2(TextWriter stream, Vector3 min, Vector3 max)
        {
            m_log.InfoFormat("[SCENE]: Saving prims in xml2 format for region {0} to stream", RegionInfo.RegionName);

            m_serialiser.SavePrimsToXml2(this, stream, min, max);
        }

        public void SaveNamedPrimsToXml2(string primName, string fileName)
        {
            m_log.InfoFormat(
                "[SCENE]: Saving prims with name {0} in xml2 format for region {1} to {2}", primName, RegionInfo.RegionName, fileName);

            List<EntityBase> entityList = GetEntities();
            List<EntityBase> primList = new List<EntityBase>();

            foreach (EntityBase ent in entityList)
            {
                if (ent is SceneObjectGroup)
                {
                    if (ent.Name == primName)
                    {
                        primList.Add(ent);
                    }
                }
            }

            m_serialiser.SavePrimListToXml2(primList, fileName);
        }

        /// <summary>
        /// Load a prim archive into the scene.  This loads both prims and their assets.
        /// </summary>
        /// <param name="filePath"></param>
        public void LoadPrimsFromArchive(string filePath)
        {
            m_log.InfoFormat("[SCENE]: Loading archive to region {0} from {1}", RegionInfo.RegionName, filePath);

            m_archiver.DearchiveRegion(filePath);
        }

        /// <summary>
        /// Save the prims in the scene to an archive.  This saves both prims and their assets.
        /// </summary>
        /// <param name="filePath"></param>
        public void SavePrimsToArchive(string filePath)
        {
            m_log.InfoFormat("[SCENE]: Writing archive for region {0} to {1}", RegionInfo.RegionName, filePath);

            m_archiver.ArchiveRegion(filePath);
        }

        /// <summary>
        /// Move the given scene object into a new region depending on which region its absolute position has moved
        /// into.
        ///
        /// This method locates the new region handle and offsets the prim position for the new region
        /// </summary>
        /// <param name="attemptedPosition">the attempted out of region position of the scene object</param>
        /// <param name="grp">the scene object that we're crossing</param>
        public void CrossPrimGroupIntoNewRegion(Vector3 attemptedPosition, SceneObjectGroup grp, bool silent)
        {
            if (grp == null)
                return;
            if (grp.IsDeleted)
                return;

            if (grp.RootPart.DIE_AT_EDGE)
            {
                // We remove the object here
                try
                {
                    DeleteSceneObject(grp, false);
                }
                catch (Exception)
                {
                    m_log.Warn("[DATABASE]: exception when trying to remove the prim that crossed the border.");
                }
                return;
            }

            int thisx = (int)RegionInfo.RegionLocX;
            int thisy = (int)RegionInfo.RegionLocY;

            ulong newRegionHandle = 0;
            Vector3 pos = attemptedPosition;

            if (attemptedPosition.X > Constants.RegionSize + 0.1f)
            {
                pos.X = ((pos.X - Constants.RegionSize));
                newRegionHandle
                    = Util.UIntsToLong((uint)((thisx + 1) * Constants.RegionSize), (uint)(thisy * Constants.RegionSize));
                // x + 1
            }
            else if (attemptedPosition.X < -0.1f)
            {
                pos.X = ((pos.X + Constants.RegionSize));
                newRegionHandle
                    = Util.UIntsToLong((uint)((thisx - 1) * Constants.RegionSize), (uint)(thisy * Constants.RegionSize));
                // x - 1
            }

            if (attemptedPosition.Y > Constants.RegionSize + 0.1f)
            {
                pos.Y = ((pos.Y - Constants.RegionSize));
                newRegionHandle
                    = Util.UIntsToLong((uint)(thisx * Constants.RegionSize), (uint)((thisy + 1) * Constants.RegionSize));
                // y + 1
            }
            else if (attemptedPosition.Y < -0.1f)
            {
                pos.Y = ((pos.Y + Constants.RegionSize));
                newRegionHandle
                    = Util.UIntsToLong((uint)(thisx * Constants.RegionSize), (uint)((thisy - 1) * Constants.RegionSize));
                // y - 1
            }

            // Offset the positions for the new region across the border
            Vector3 oldGroupPosition = grp.RootPart.GroupPosition;
            grp.OffsetForNewRegion(pos);

            // If we fail to cross the border, then reset the position of the scene object on that border.
            if (!CrossPrimGroupIntoNewRegion(newRegionHandle, grp, silent))
            {
                grp.OffsetForNewRegion(oldGroupPosition);
            }
        }

        /// <summary>
        /// Move the given scene object into a new region
        /// </summary>
        /// <param name="newRegionHandle"></param>
        /// <param name="grp">Scene Object Group that we're crossing</param>
        /// <returns>
        /// true if the crossing itself was successful, false on failure
        /// FIMXE: we still return true if the crossing object was not successfully deleted from the originating region
        /// </returns>
        public bool CrossPrimGroupIntoNewRegion(ulong newRegionHandle, SceneObjectGroup grp, bool silent)
        {
            bool successYN = false;
            grp.RootPart.UpdateFlag = 0;
            int primcrossingXMLmethod = 0;

            if (newRegionHandle != 0)
            {
                successYN
                    = m_sceneGridService.PrimCrossToNeighboringRegion(
                        newRegionHandle, grp.UUID, m_serialiser.SaveGroupToXml2(grp), primcrossingXMLmethod);

                if (successYN)
                {
                    // We remove the object here
                    try
                    {
                        DeleteSceneObject(grp, silent);
                    }
                    catch (Exception e)
                    {
                        m_log.ErrorFormat(
                            "[INTERREGION]: Exception deleting the old object left behind on a border crossing for {0}, {1}",
                            grp, e);
                    }
                }
                else
                {
                    if (!grp.IsDeleted)
                    {
                        if (grp.RootPart.PhysActor != null)
                        {
                            grp.RootPart.PhysActor.CrossingFailure();
                        }
                    }

                    m_log.ErrorFormat("[INTERREGION]: Prim crossing failed for {0}", grp);
                }
            }
            else
            {
                m_log.Error("[INTERREGION]: region handle was unexpectedly 0 in Scene.CrossPrimGroupIntoNewRegion()");
            }

            return successYN;
        }

        /// <summary>
        /// Handle a scene object that is crossing into this region from another.
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="primID"></param>
        /// <param name="objXMLData"></param>
        /// <param name="XMLMethod"></param>
        /// <returns></returns>
        public bool IncomingInterRegionPrimGroup(UUID primID, string objXMLData, int XMLMethod)
        {
            m_log.DebugFormat("[INTERREGION]: A new prim {0} arrived from a neighbor", primID);
            
            if (XMLMethod == 0)
            {
                SceneObjectGroup sceneObject = m_serialiser.DeserializeGroupFromXml2(objXMLData);
                AddRestoredSceneObject(sceneObject, true, false);

                SceneObjectPart RootPrim = GetSceneObjectPart(primID);
                if (RootPrim != null)
                {
                    if (m_regInfo.EstateSettings.IsBanned(RootPrim.OwnerID))
                    {
                        SceneObjectGroup grp = RootPrim.ParentGroup;
                        if (grp != null)
                        {
                            DeleteSceneObject(grp, false);
                        }

                        m_log.Info("[INTERREGION]: Denied prim crossing for banned avatar");

                        return false;
                    }
                    if (RootPrim.Shape.PCode == (byte)PCode.Prim)
                    {
                        SceneObjectGroup grp = RootPrim.ParentGroup;
                        if (grp != null)
                        {
                            if (RootPrim.Shape.State != 0)
                            {
                                // Never persist

                                m_log.DebugFormat("[ATTACHMENT]: Received attachment {0}, inworld asset id {1}", grp.RootPart.LastOwnerID.ToString(), grp.UUID.ToString());

                                grp.DetachFromBackup();

                                // Attachment
                                ScenePresence sp = GetScenePresence(grp.OwnerID);
                                if (sp != null)
                                {
                                    // hack assetID until we get assetID into the XML format.
                                    // LastOwnerID is used for group deeding, so when you do stuff
                                    // with the deeded object, it goes back to them

                                    grp.SetFromAssetID(grp.RootPart.LastOwnerID);
                                    m_log.DebugFormat("[ATTACHMENT]: Attach to avatar {0}", sp.UUID.ToString());
                                    AttachObject(sp.ControllingClient, grp.LocalId, (uint)0, grp.GroupRotation, grp.AbsolutePosition, false);
                                }
                                else
                                {
                                    // Remove, then add, to ensure the expire
                                    // time is refreshed. Wouldn't do to
                                    // have it poof before the avatar gets
                                    // there.
                                    //
                                    RootPrim.RemFlag(PrimFlags.TemporaryOnRez);
                                    RootPrim.AddFlag(PrimFlags.TemporaryOnRez);
                                }
                            }
                        }
                    }
                }
                return true;
            }
            else
            {
                return false;
            }
        }

        #endregion

        #region Add/Remove Avatar Methods

        public override void AddNewClient(IClientAPI client, bool child)
        {
            SubscribeToClientEvents(client);
            ScenePresence presence;

            if (m_restorePresences.ContainsKey(client.AgentId))
            {
                m_log.DebugFormat("[SCENE]: Restoring agent {0} {1} in {2}", client.Name, client.AgentId, RegionInfo.RegionName);

                presence = m_restorePresences[client.AgentId];
                m_restorePresences.Remove(client.AgentId);

                // This is one of two paths to create avatars that are
                // used.  This tends to get called more in standalone
                // than grid, not really sure why, but as such needs
                // an explicity appearance lookup here.
                AvatarAppearance appearance = null;
                GetAvatarAppearance(client, out appearance);
                presence.Appearance = appearance;

                presence.initializeScenePresence(client, RegionInfo, this);

                m_sceneGraph.AddScenePresence(presence);

                lock (m_restorePresences)
                {
                    Monitor.PulseAll(m_restorePresences);
                }
            }
            else
            {
                m_log.DebugFormat(
                    "[SCENE]: Adding new {0} agent {1} {2} in {3}",
                    (child ? "child" : "root"), client.Name, client.AgentId, RegionInfo.RegionName);

                CommsManager.UserProfileCacheService.AddNewUser(client.AgentId);

                CreateAndAddScenePresence(client, child);
            }
            m_LastLogin = System.Environment.TickCount;
            EventManager.TriggerOnNewClient(client);
        }

        protected virtual void SubscribeToClientEvents(IClientAPI client)
        {
            client.OnRegionHandShakeReply += SendLayerData;
            client.OnAddPrim += AddNewPrim;
            client.OnUpdatePrimGroupPosition += m_sceneGraph.UpdatePrimPosition;
            client.OnUpdatePrimSinglePosition += m_sceneGraph.UpdatePrimSinglePosition;
            client.OnUpdatePrimGroupRotation += m_sceneGraph.UpdatePrimRotation;
            client.OnUpdatePrimGroupMouseRotation += m_sceneGraph.UpdatePrimRotation;
            client.OnUpdatePrimSingleRotation += m_sceneGraph.UpdatePrimSingleRotation;
            client.OnUpdatePrimScale += m_sceneGraph.UpdatePrimScale;
            client.OnUpdatePrimGroupScale += m_sceneGraph.UpdatePrimGroupScale;
            client.OnUpdateExtraParams += m_sceneGraph.UpdateExtraParam;
            client.OnUpdatePrimShape += m_sceneGraph.UpdatePrimShape;
            //client.OnRequestMapBlocks += RequestMapBlocks; // handled in a module now.
            client.OnUpdatePrimTexture += m_sceneGraph.UpdatePrimTexture;
            client.OnTeleportLocationRequest += RequestTeleportLocation;
            client.OnTeleportLandmarkRequest += RequestTeleportLandmark;
            client.OnObjectSelect += SelectPrim;
            client.OnObjectDeselect += DeselectPrim;
            client.OnGrabUpdate += m_sceneGraph.MoveObject;
            client.OnDeRezObject += DeRezObject;
            client.OnRezObject += RezObject;
            client.OnRezSingleAttachmentFromInv += RezSingleAttachment;
            client.OnDetachAttachmentIntoInv += DetachSingleAttachmentToInv;
            client.OnObjectAttach += m_sceneGraph.AttachObject;
            client.OnObjectDetach += m_sceneGraph.DetachObject;
            client.OnObjectDrop += m_sceneGraph.DropObject;
            client.OnNameFromUUIDRequest += CommsManager.HandleUUIDNameRequest;
            client.OnObjectDescription += m_sceneGraph.PrimDescription;
            client.OnObjectName += m_sceneGraph.PrimName;
            client.OnObjectClickAction += m_sceneGraph.PrimClickAction;
            client.OnObjectMaterial += m_sceneGraph.PrimMaterial;
            client.OnLinkObjects += m_sceneGraph.LinkObjects;
            client.OnDelinkObjects += m_sceneGraph.DelinkObjects;
            client.OnObjectDuplicate += m_sceneGraph.DuplicateObject;
            client.OnObjectDuplicateOnRay += doObjectDuplicateOnRay;
            client.OnUpdatePrimFlags += m_sceneGraph.UpdatePrimFlags;
            client.OnRequestObjectPropertiesFamily += m_sceneGraph.RequestObjectPropertiesFamily;
            client.OnRequestGodlikePowers += handleRequestGodlikePowers;
            client.OnGodKickUser += HandleGodlikeKickUser;
            client.OnObjectPermissions += HandleObjectPermissionsUpdate;
            client.OnCreateNewInventoryItem += CreateNewInventoryItem;
            client.OnCreateNewInventoryFolder += CommsManager.UserProfileCacheService.HandleCreateInventoryFolder;
            client.OnUpdateInventoryFolder += CommsManager.UserProfileCacheService.HandleUpdateInventoryFolder;
            client.OnMoveInventoryFolder += CommsManager.UserProfileCacheService.HandleMoveInventoryFolder;
            client.OnFetchInventoryDescendents += CommsManager.UserProfileCacheService.HandleFetchInventoryDescendents;
            client.OnPurgeInventoryDescendents += CommsManager.UserProfileCacheService.HandlePurgeInventoryDescendents;
            client.OnFetchInventory += CommsManager.UserProfileCacheService.HandleFetchInventory;
            client.OnUpdateInventoryItem += UpdateInventoryItemAsset;
            client.OnCopyInventoryItem += CopyInventoryItem;
            client.OnMoveInventoryItem += MoveInventoryItem;
            client.OnRemoveInventoryItem += RemoveInventoryItem;
            client.OnRemoveInventoryFolder += RemoveInventoryFolder;
            client.OnRezScript += RezScript;
            client.OnRequestTaskInventory += RequestTaskInventory;
            client.OnRemoveTaskItem += RemoveTaskInventory;
            client.OnUpdateTaskInventory += UpdateTaskInventory;
            client.OnMoveTaskItem += ClientMoveTaskInventoryItem;
            client.OnGrabObject += ProcessObjectGrab;
            client.OnDeGrabObject += ProcessObjectDeGrab;
            client.OnMoneyTransferRequest += ProcessMoneyTransferRequest;
            client.OnParcelBuy += ProcessParcelBuy;
            client.OnAvatarPickerRequest += ProcessAvatarPickerRequest;
            client.OnObjectIncludeInSearch += m_sceneGraph.MakeObjectSearchable;
            client.OnTeleportHomeRequest += TeleportClientHome;
            client.OnSetStartLocationRequest += SetHomeRezPoint;
            client.OnUndo += m_sceneGraph.HandleUndo;
            client.OnObjectGroupRequest += m_sceneGraph.HandleObjectGroupUpdate;
            client.OnParcelReturnObjectsRequest += LandChannel.ReturnObjectsInParcel;
            client.OnParcelSetOtherCleanTime += LandChannel.SetParcelOtherCleanTime;
            client.OnObjectSaleInfo += ObjectSaleInfo;
            client.OnScriptReset += ProcessScriptReset;
            client.OnGetScriptRunning += GetScriptRunning;
            client.OnSetScriptRunning += SetScriptRunning;
            client.OnRegionHandleRequest += RegionHandleRequest;
            client.OnUnackedTerrain += TerrainUnAcked;

            //Gesture
            client.OnActivateGesture += ActivateGesture;
            client.OnDeactivateGesture += DeactivateGesture;
            //sound
            client.OnSoundTrigger += SoundTrigger;

            client.OnObjectOwner += ObjectOwner;

            // EventManager.TriggerOnNewClient(client);
        }

        // Sound
        public virtual void SoundTrigger( UUID soundId, UUID ownerID, UUID objectID, UUID parentID,
                                                float gain, Vector3 position, UInt64 handle)
        {
            foreach (ScenePresence p in GetAvatars())
            {
                double dis = Util.GetDistanceTo(p.AbsolutePosition, position);
                if (dis > 100.0) // Max audio distance
                    continue;

                // Scale by distance          
                gain = (float)((double)gain*((100.0 - dis) / 100.0));
                p.ControllingClient.SendTriggeredSound(soundId, ownerID, objectID, parentID, handle, position, (float)gain);
            }
            
        }

        // Gesture
        public virtual void ActivateGesture(IClientAPI client,  UUID assetId, UUID gestureId)
        {
          //  UserProfileCacheService User = CommsManager.SecureInventoryService.UpdateItem(gestureid, agentID);
            CachedUserInfo userInfo = CommsManager.UserProfileCacheService.GetUserDetails(client.AgentId);

            if (userInfo != null)
            {
                InventoryItemBase item = userInfo.RootFolder.FindItem(gestureId);
                if (item != null)
                {
                    item.Flags = 1;
                    userInfo.UpdateItem(item);
                }
                else m_log.Error("Unable to find gesture");
            }
            else m_log.Error("Gesture : Unable to find user ");

            m_log.DebugFormat("Asset : {0} gesture :{1}", gestureId.ToString(), assetId.ToString());
        }

        public virtual void DeactivateGesture(IClientAPI client,  UUID gestureId)
        {
            //  UserProfileCacheService User = CommsManager.SecureInventoryService.UpdateItem(gestureid, agentID);
            CachedUserInfo userInfo = CommsManager.UserProfileCacheService.GetUserDetails(client.AgentId);

            if (userInfo != null)
            {
                InventoryItemBase item = userInfo.RootFolder.FindItem(gestureId);
                if (item != null)
                {
                    item.Flags = 0;
                    userInfo.UpdateItem(item);
                }
                else m_log.Error("Unable to find gesture");
            }
            else m_log.Error("Gesture : Unable to find user ");

            m_log.DebugFormat("gesture : {0} ", gestureId.ToString());
        }

        /// <summary>
        /// Teleport an avatar to their home region
        /// </summary>
        /// <param name="agentId"></param>
        /// <param name="client"></param>
        public virtual void TeleportClientHome(UUID agentId, IClientAPI client)
        {
            UserProfileData UserProfile = CommsManager.UserService.GetUserProfile(agentId);
            if (UserProfile != null)
            {
                RegionInfo regionInfo = CommsManager.GridService.RequestNeighbourInfo(UserProfile.HomeRegionID);
                if (regionInfo == null)
                {
                    regionInfo = CommsManager.GridService.RequestNeighbourInfo(UserProfile.HomeRegion);
                    if (regionInfo != null) // home region can be away temporarily, too
                    {
                        UserProfile.HomeRegionID = regionInfo.RegionID;
                        CommsManager.UserService.UpdateUserProfile(UserProfile);
                    }
                }
                if (regionInfo == null)
                {
                    // can't find the Home region: Tell viewer and abort
                    client.SendTeleportFailed("Your home-region could not be found.");
                    return;
                }
                RequestTeleportLocation(
                    client, regionInfo.RegionHandle, UserProfile.HomeLocation, UserProfile.HomeLookAt,
                    (uint)(TPFlags.SetLastToTarget | TPFlags.ViaHome));
            }
        }

        public void doObjectDuplicateOnRay(uint localID, uint dupeFlags, UUID AgentID, UUID GroupID,
                                           UUID RayTargetObj, Vector3 RayEnd, Vector3 RayStart,
                                           bool BypassRaycast, bool RayEndIsIntersection, bool CopyCenters, bool CopyRotates)
        {
            Vector3 pos;
            const bool frontFacesOnly = true;
            //m_log.Info("HITTARGET: " + RayTargetObj.ToString() + ", COPYTARGET: " + localID.ToString());
            SceneObjectPart target = GetSceneObjectPart(localID);
            SceneObjectPart target2 = GetSceneObjectPart(RayTargetObj);

            if (target != null && target2 != null)
            {
                Vector3 direction = Vector3.Normalize(RayEnd - RayStart);
                Vector3 AXOrigin = new Vector3(RayStart.X, RayStart.Y, RayStart.Z);
                Vector3 AXdirection = new Vector3(direction.X, direction.Y, direction.Z);

                if (target2.ParentGroup != null)
                {
                    pos = target2.AbsolutePosition;
                    //m_log.Info("[OBJECT_REZ]: TargetPos: " + pos.ToString() + ", RayStart: " + RayStart.ToString() + ", RayEnd: " + RayEnd.ToString() + ", Volume: " + Util.GetDistanceTo(RayStart,RayEnd).ToString() + ", mag1: " + Util.GetMagnitude(RayStart).ToString() + ", mag2: " + Util.GetMagnitude(RayEnd).ToString());

                    // TODO: Raytrace better here

                    //EntityIntersection ei = m_sceneGraph.GetClosestIntersectingPrim(new Ray(AXOrigin, AXdirection));
                    Ray NewRay = new Ray(AXOrigin, AXdirection);

                    // Ray Trace against target here
                    EntityIntersection ei = target2.TestIntersectionOBB(NewRay, Quaternion.Identity, frontFacesOnly, CopyCenters);

                    // Un-comment out the following line to Get Raytrace results printed to the console.
                    //m_log.Info("[RAYTRACERESULTS]: Hit:" + ei.HitTF.ToString() + " Point: " + ei.ipoint.ToString() + " Normal: " + ei.normal.ToString());
                    float ScaleOffset = 0.5f;

                    // If we hit something
                    if (ei.HitTF)
                    {
                        Vector3 scale = target.Scale;
                        Vector3 scaleComponent = new Vector3(ei.AAfaceNormal.X, ei.AAfaceNormal.Y, ei.AAfaceNormal.Z);
                        if (scaleComponent.X != 0) ScaleOffset = scale.X;
                        if (scaleComponent.Y != 0) ScaleOffset = scale.Y;
                        if (scaleComponent.Z != 0) ScaleOffset = scale.Z;
                        ScaleOffset = Math.Abs(ScaleOffset);
                        Vector3 intersectionpoint = new Vector3(ei.ipoint.X, ei.ipoint.Y, ei.ipoint.Z);
                        Vector3 normal = new Vector3(ei.normal.X, ei.normal.Y, ei.normal.Z);
                        Vector3 offset = normal * (ScaleOffset / 2f);
                        pos = intersectionpoint + offset;

                        // stick in offset format from the original prim
                        pos = pos - target.ParentGroup.AbsolutePosition;
                        if (CopyRotates)
                        {
                            Quaternion worldRot = target2.GetWorldRotation();

                            // SceneObjectGroup obj = m_sceneGraph.DuplicateObject(localID, pos, target.GetEffectiveObjectFlags(), AgentID, GroupID, worldRot);
                            m_sceneGraph.DuplicateObject(localID, pos, target.GetEffectiveObjectFlags(), AgentID, GroupID, worldRot);
                            //obj.Rotation = worldRot;
                            //obj.UpdateGroupRotation(worldRot);
                        }
                        else
                        {
                            m_sceneGraph.DuplicateObject(localID, pos, target.GetEffectiveObjectFlags(), AgentID, GroupID);
                        }
                    }

                    return;
                }

                return;
            }
        }

        public virtual void SetHomeRezPoint(IClientAPI remoteClient, ulong regionHandle, Vector3 position, Vector3 lookAt, uint flags)
        {
            UserProfileData UserProfile = CommsManager.UserService.GetUserProfile(remoteClient.AgentId);
            if (UserProfile != null)
            {
                // I know I'm ignoring the regionHandle provided by the teleport location request.
                // reusing the TeleportLocationRequest delegate, so regionHandle isn't valid
                UserProfile.HomeRegionID = RegionInfo.RegionID;
                // TODO: The next line can be removed, as soon as only homeRegionID based UserServers are around.
                // TODO: The HomeRegion property can be removed then, too
                UserProfile.HomeRegion = RegionInfo.RegionHandle;
                UserProfile.HomeLocation = position;
                UserProfile.HomeLookAt = lookAt;
                CommsManager.UserService.UpdateUserProfile(UserProfile);

                // FUBAR ALERT: this needs to be "Home position set." so the viewer saves a home-screenshot.
                remoteClient.SendAgentAlertMessage("Home position set.",false);
            }
            else
            {
                remoteClient.SendAgentAlertMessage("Set Home request Failed",false);
            }
        }

        /// <summary>
        /// Create a scene presence and add it to this scene.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="child"></param>
        /// <returns></returns>
        protected virtual ScenePresence CreateAndAddScenePresence(IClientAPI client, bool child)
        {
            AvatarAppearance appearance = null;
            GetAvatarAppearance(client, out appearance);

            ScenePresence avatar = m_sceneGraph.CreateAndAddScenePresence(client, child, appearance);

            return avatar;
        }

        /// <summary>
        /// Get the avatar apperance for the given client.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="appearance"></param>
        public void GetAvatarAppearance(IClientAPI client, out AvatarAppearance appearance)
        {
            appearance = new AvatarAppearance();

            try
            {
                if (m_AvatarFactory != null)
                {
                    if (m_AvatarFactory.TryGetAvatarAppearance(client.AgentId, out appearance))
                        return;
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[APPEARANCE]: Problem fetching appearance for avatar {0}, {1}",
                    client.Name, e);
            }
            
            m_log.Warn("[APPEARANCE]: Appearance not found, returning default");
        }

        /// <summary>
        /// Remove the given client from the scene.
        /// </summary>
        /// <param name="agentID"></param>
        public override void RemoveClient(UUID agentID)
        {
            bool childagentYN = false;
            ScenePresence avatar = GetScenePresence(agentID);
            if (avatar != null)
            {
                childagentYN = avatar.IsChildAgent;
            }

            try
            {
                m_log.DebugFormat(
                    "[SCENE]: Removing {0} agent {1} from region {2}",
                    (childagentYN ? "child" : "root"), agentID, RegionInfo.RegionName);

                if (avatar.IsChildAgent)
                {
                    m_sceneGraph.removeUserCount(false);
                }
                else
                {
                    m_sceneGraph.removeUserCount(true);
                    m_sceneGridService.LogOffUser(agentID, RegionInfo.RegionID, RegionInfo.RegionHandle, avatar.AbsolutePosition, avatar.Lookat);
                    List<ulong> childknownRegions = new List<ulong>();
                    List<ulong> ckn = avatar.GetKnownRegionList();
                    for (int i = 0; i < ckn.Count; i++)
                    {
                        childknownRegions.Add(ckn[i]);
                    }
                    m_sceneGridService.SendCloseChildAgentConnections(agentID, childknownRegions);

                    RemoveCapsHandler(agentID);

                    CommsManager.UserProfileCacheService.RemoveUser(agentID);
                }

                m_eventManager.TriggerClientClosed(agentID);
            }
            catch (NullReferenceException)
            {
                // We don't know which count to remove it from
                // Avatar is already disposed :/
            }

            m_eventManager.TriggerOnRemovePresence(agentID);
            Broadcast(delegate(IClientAPI client)
                      {
                          try
                          {
                              client.SendKillObject(avatar.RegionHandle, avatar.LocalId);
                          }
                          catch (NullReferenceException)
                          {
                              //We can safely ignore null reference exceptions.  It means the avatar are dead and cleaned up anyway.
                          }
                      });

            ForEachScenePresence(
                delegate(ScenePresence presence) { presence.CoarseLocationChange(); });

            IAgentAssetTransactions agentTransactions = this.RequestModuleInterface<IAgentAssetTransactions>();
            if (agentTransactions != null)
            {
                agentTransactions.RemoveAgentAssetTransactions(agentID);
            }

            m_sceneGraph.RemoveScenePresence(agentID);

            try
            {
                avatar.Close();
            }
            catch (NullReferenceException)
            {
                //We can safely ignore null reference exceptions.  It means the avatar are dead and cleaned up anyway.
            }
            catch (Exception e)
            {
                m_log.Error("[SCENE] Scene.cs:RemoveClient exception: " + e.ToString());
            }

            // Remove client agent from profile, so new logins will work
            if (!childagentYN)
            {
                m_sceneGridService.ClearUserAgent(agentID);
            }

            //m_log.InfoFormat("[SCENE] Memory pre  GC {0}", System.GC.GetTotalMemory(false));
            //m_log.InfoFormat("[SCENE] Memory post GC {0}", System.GC.GetTotalMemory(true));
        }

        public void HandleRemoveKnownRegionsFromAvatar(UUID avatarID, List<ulong> regionslst)
        {
            ScenePresence av = GetScenePresence(avatarID);
            if (av != null)
            {
                lock (av)
                {

                    for (int i = 0; i < regionslst.Count; i++)
                    {
                        av.KnownChildRegions.Remove(regionslst[i]);
                    }
                }
            }
        }

        public override void CloseAllAgents(uint circuitcode)
        {
            // Called by ClientView to kill all circuit codes
            ClientManager.CloseAllAgents(circuitcode);
        }

        public void NotifyMyCoarseLocationChange()
        {
            ForEachScenePresence(delegate(ScenePresence presence) { presence.CoarseLocationChange(); });
        }

        #endregion

        #region Entities

        public void SendKillObject(uint localID)
        {
            SceneObjectPart part = GetSceneObjectPart(localID);
            if (part != null) // It is a prim
            {
                if (part.ParentGroup != null && !part.ParentGroup.IsDeleted) // Valid
                {
                    if (part.ParentGroup.RootPart != part) // Child part
                        return;
                }
            }
            Broadcast(delegate(IClientAPI client) { client.SendKillObject(m_regionHandle, localID); });
        }

        #endregion

        #region RegionComms

        /// <summary>
        /// Register the methods that should be invoked when this scene receives various incoming events
        /// </summary>
        public void RegisterCommsEvents()
        {
            m_sceneGridService.OnExpectUser += NewUserConnection;
            m_sceneGridService.OnAvatarCrossingIntoRegion += AgentCrossing;
            m_sceneGridService.OnCloseAgentConnection += CloseConnection;
            m_sceneGridService.OnRegionUp += OtherRegionUp;
            m_sceneGridService.OnChildAgentUpdate += IncomingChildAgentDataUpdate;
            m_sceneGridService.OnExpectPrim += IncomingInterRegionPrimGroup;
            m_sceneGridService.OnRemoveKnownRegionFromAvatar += HandleRemoveKnownRegionsFromAvatar;
            m_sceneGridService.OnLogOffUser += HandleLogOffUserFromGrid;
            m_sceneGridService.KiPrimitive += SendKillObject;
            m_sceneGridService.OnGetLandData += GetLandData;
        }

        /// <summary>
        /// Deregister this scene from receiving incoming region events
        /// </summary>
        public void UnRegisterRegionWithComms()
        {
            m_sceneGridService.KiPrimitive -= SendKillObject;
            m_sceneGridService.OnLogOffUser -= HandleLogOffUserFromGrid;
            m_sceneGridService.OnRemoveKnownRegionFromAvatar -= HandleRemoveKnownRegionsFromAvatar;
            m_sceneGridService.OnExpectPrim -= IncomingInterRegionPrimGroup;
            m_sceneGridService.OnChildAgentUpdate -= IncomingChildAgentDataUpdate;
            m_sceneGridService.OnRegionUp -= OtherRegionUp;
            m_sceneGridService.OnExpectUser -= NewUserConnection;
            m_sceneGridService.OnAvatarCrossingIntoRegion -= AgentCrossing;
            m_sceneGridService.OnCloseAgentConnection -= CloseConnection;
            m_sceneGridService.OnGetLandData -= GetLandData;

            m_sceneGridService.Close();
        }

        /// <summary>
        /// Do the work necessary to initiate a new user connection for a particular scene.
        /// At the moment, this consists of setting up the caps infrastructure
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="agent"></param>
        public void NewUserConnection(AgentCircuitData agent)
        {
            m_log.DebugFormat("[CONNECTION DEBUGGING] Adding NewUserConnection for {0} with CC of {1}", agent.AgentID,
                              agent.circuitcode);

            if (m_regInfo.EstateSettings.IsBanned(agent.AgentID))
            {
                m_log.WarnFormat(
               "[CONNECTION DEBUGGING]: Denied access to: {0} at {1} because the user is on the region banlist",
               agent.AgentID, RegionInfo.RegionName);
            }

            capsPaths[agent.AgentID] = agent.CapsPath;

            if (!agent.child)
            {
                AddCapsHandler(agent.AgentID);

                // Honor parcel landing type and position.
                ILandObject land = LandChannel.GetLandObject(agent.startpos.X, agent.startpos.Y);
                if (land != null)
                {
                    if (land.landData.LandingType == (byte)1 && land.landData.UserLocation != Vector3.Zero)
                    {
                        agent.startpos = land.landData.UserLocation;
                    }
                }
            }

            m_authenticateHandler.AddNewCircuit(agent.circuitcode, agent);
            // rewrite session_id
            CachedUserInfo userinfo = CommsManager.UserProfileCacheService.GetUserDetails(agent.AgentID);
            if (userinfo != null)
            {
                userinfo.SessionID = agent.SessionID;
            }
            else
            {
                m_log.WarnFormat("[USERINFO CACHE]: We couldn't find a User Info record for {0}.  This is usually an indication that the UUID we're looking up is invalid", agent.AgentID);
            }
        }

        public void UpdateCircuitData(AgentCircuitData data)
        {
            m_authenticateHandler.UpdateAgentData(data);
        }

        public bool ChangeCircuitCode(uint oldcc, uint newcc)
        {
            return m_authenticateHandler.TryChangeCiruitCode(oldcc, newcc);
        }

        protected void HandleLogOffUserFromGrid(UUID AvatarID, UUID RegionSecret, string message)
        {
            ScenePresence loggingOffUser = null;
            loggingOffUser = GetScenePresence(AvatarID);
            if (loggingOffUser != null)
            {
                if (RegionSecret == loggingOffUser.ControllingClient.SecureSessionId)
                {
                    loggingOffUser.ControllingClient.Kick(message);
                    // Give them a second to receive the message!
                    System.Threading.Thread.Sleep(1000);
                    loggingOffUser.ControllingClient.Close(true);
                }
                else
                {
                    m_log.Info("[USERLOGOFF]: System sending the LogOff user message failed to sucessfully authenticate");
                }
            }
            else
            {
                m_log.InfoFormat("[USERLOGOFF]: Got a logoff request for {0} but the user isn't here.  The user might already have been logged out", AvatarID.ToString());
            }
        }

        /// <summary>
        /// Add a caps handler for the given agent.  If the CAPS handler already exists for this agent,
        /// then it is replaced by a new CAPS handler.
        ///
        /// FIXME: On login this is called twice, once for the login and once when the connection is made.
        /// This is somewhat innefficient and should be fixed.  The initial login creation is necessary
        /// since the client asks for capabilities immediately after being informed of the seed.
        /// </summary>
        /// <param name="agentId"></param>
        /// <param name="capsObjectPath"></param>
        public void AddCapsHandler(UUID agentId)
        {
            if (RegionInfo.EstateSettings.IsBanned(agentId))
                return;
            String capsObjectPath = GetCapsPath(agentId);

            m_log.DebugFormat(
                "[CAPS]: Setting up CAPS handler for root agent {0} in {1}",
                agentId, RegionInfo.RegionName);

            Caps cap =
                new Caps(AssetCache, m_httpListener, m_regInfo.ExternalHostName, m_httpListener.Port,
                         capsObjectPath, agentId, m_dumpAssetsToFile, RegionInfo.RegionName);
            cap.RegisterHandlers();

            EventManager.TriggerOnRegisterCaps(agentId, cap);

            cap.AddNewInventoryItem = AddUploadedInventoryItem;
            cap.ItemUpdatedCall = CapsUpdateInventoryItemAsset;
            cap.TaskScriptUpdatedCall = CapsUpdateTaskInventoryScriptAsset;
            cap.CAPSFetchInventoryDescendents = CommsManager.UserProfileCacheService.HandleFetchInventoryDescendentsCAPS;
            cap.GetClient = m_sceneGraph.GetControllingClient;
            m_capsHandlers[agentId] = cap;
        }

        public Caps GetCapsHandlerForUser(UUID agentId)
        {
            lock (m_capsHandlers)
            {
                if (m_capsHandlers.ContainsKey(agentId))
                {
                    return m_capsHandlers[agentId];
                }
            }
            return null;
        }

        /// <summary>
        /// Remove the caps handler for a given agent.
        /// </summary>
        /// <param name="agentId"></param>
        public void RemoveCapsHandler(UUID agentId)
        {
            lock (m_capsHandlers)
            {
                if (m_capsHandlers.ContainsKey(agentId))
                {
                    m_log.DebugFormat(
                        "[CAPS]: Removing CAPS handler for root agent {0} in {1}",
                        agentId, RegionInfo.RegionName);

                    m_capsHandlers[agentId].DeregisterHandlers();
                    EventManager.TriggerOnDeregisterCaps(agentId, m_capsHandlers[agentId]);

                    m_capsHandlers.Remove(agentId);
                }
                else
                {
                    m_log.WarnFormat(
                        "[CAPS]: Received request to remove CAPS handler for root agent {0} in {1}, but no such CAPS handler found!",
                        agentId, RegionInfo.RegionName);
                }
            }
        }

        /// <summary>
        /// Triggered when an agent crosses into this sim.  Also happens on initial login.
        /// </summary>
        /// <param name="agentID"></param>
        /// <param name="position"></param>
        /// <param name="isFlying"></param>
        public virtual void AgentCrossing(UUID agentID, Vector3 position, bool isFlying)
        {
            ScenePresence presence;
            
            lock (m_scenePresences)
            {
                m_scenePresences.TryGetValue(agentID, out presence);
            }
            
            if (presence != null)
            {
                try
                {
                    presence.MakeRootAgent(position, isFlying);
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[SCENE]: Unable to do agent crossing, exception {0}", e);
                }
            }
            else
            {
                m_log.ErrorFormat(
                    "[SCENE]: Could not find presence for agent {0} crossing into scene {1}",
                    agentID, RegionInfo.RegionName);
            }
        }

        public virtual bool IncomingChildAgentDataUpdate(ChildAgentDataUpdate cAgentData)
        {
            ScenePresence childAgentUpdate = GetScenePresence(new UUID(cAgentData.AgentID));
            if (childAgentUpdate != null)
            {
                // I can't imagine *yet* why we would get an update if the agent is a root agent..
                // however to avoid a race condition crossing borders..
                if (childAgentUpdate.IsChildAgent)
                {
                    uint rRegionX = (uint)(cAgentData.regionHandle >> 40);
                    uint rRegionY = (((uint)(cAgentData.regionHandle)) >> 8);
                    uint tRegionX = RegionInfo.RegionLocX;
                    uint tRegionY = RegionInfo.RegionLocY;
                    //Send Data to ScenePresence
                    childAgentUpdate.ChildAgentDataUpdate(cAgentData, tRegionX, tRegionY, rRegionX, rRegionY);
                    // Not Implemented:
                    //TODO: Do we need to pass the message on to one of our neighbors?
                }
                
                return true;
            }
            
            return false;
        }

        /// <summary>
        /// Tell a single agent to disconnect from the region.
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="agentID"></param>
        public bool CloseConnection(UUID agentID)
        {
            ScenePresence presence = m_sceneGraph.GetScenePresence(agentID);
            
            if (presence != null)
            {
                // Nothing is removed here, so down count it as such
                // if (presence.IsChildAgent)
                // {
                //    m_sceneGraph.removeUserCount(false);
                // }
                // else
                // {
                //    m_sceneGraph.removeUserCount(true);
                // }

                // Tell a single agent to disconnect from the region.
                presence.ControllingClient.SendShutdownConnectionNotice();

                presence.ControllingClient.Close(true);
            }
            
            return true;
        }

        /// <summary>
        /// Tell neighboring regions about this agent
        /// When the regions respond with a true value,
        /// tell the agents about the region.
        ///
        /// We have to tell the regions about the agents first otherwise it'll deny them access
        ///
        /// </summary>
        /// <param name="presence"></param>
        public void InformClientOfNeighbours(ScenePresence presence)
        {
            m_sceneGridService.EnableNeighbourChildAgents(presence, m_neighbours);
        }

        /// <summary>
        /// Tell a neighboring region about this agent
        /// </summary>
        /// <param name="presence"></param>
        /// <param name="region"></param>
        public void InformClientOfNeighbor(ScenePresence presence, RegionInfo region)
        {
            m_sceneGridService.InformNeighborChildAgent(presence, region, m_neighbours);
        }

        /// <summary>
        /// Requests information about this region from gridcomms
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <returns></returns>
        public RegionInfo RequestNeighbouringRegionInfo(ulong regionHandle)
        {
            return m_sceneGridService.RequestNeighbouringRegionInfo(regionHandle);
        }

        /// <summary>
        /// Requests textures for map from minimum region to maximum region in world cordinates
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="minX"></param>
        /// <param name="minY"></param>
        /// <param name="maxX"></param>
        /// <param name="maxY"></param>
        public void RequestMapBlocks(IClientAPI remoteClient, int minX, int minY, int maxX, int maxY)
        {
            m_log.InfoFormat("[MAPBLOCK]: {0}-{1}, {2}-{3}", minX, minY, maxX, maxY);
            m_sceneGridService.RequestMapBlocks(remoteClient, minX, minY, maxX, maxY);
        }

        /// <summary>
        /// Tries to teleport agent to other region.
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="regionName"></param>
        /// <param name="position"></param>
        /// <param name="lookAt"></param>
        /// <param name="teleportFlags"></param>
        public void RequestTeleportLocation(IClientAPI remoteClient, string regionName, Vector3 position,
                                            Vector3 lookat, uint teleportFlags)
        {
            RegionInfo regionInfo = m_sceneGridService.RequestClosestRegion(regionName);
            if (regionInfo == null)
            {
                // can't find the region: Tell viewer and abort
                remoteClient.SendTeleportFailed("The region '" + regionName + "' could not be found.");
                return;
            }
            RequestTeleportLocation(remoteClient, regionInfo.RegionHandle, position, lookat, teleportFlags);
        }

        /// <summary>
        /// Tries to teleport agent to other region.
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="regionHandle"></param>
        /// <param name="position"></param>
        /// <param name="lookAt"></param>
        /// <param name="teleportFlags"></param>
        public void RequestTeleportLocation(IClientAPI remoteClient, ulong regionHandle, Vector3 position,
                                            Vector3 lookAt, uint teleportFlags)
        {
            lock (m_scenePresences)
            {
                if (m_scenePresences.ContainsKey(remoteClient.AgentId))
                {
                    m_sceneGridService.RequestTeleportToLocation(m_scenePresences[remoteClient.AgentId], regionHandle,
                                                                 position, lookAt, teleportFlags);
                }
            }
        }

        /// <summary>
        /// Tries to teleport agent to landmark.
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="regionHandle"></param>
        /// <param name="position"></param>
        public void RequestTeleportLandmark(IClientAPI remoteClient, UUID regionID, Vector3 position)
        {
            RegionInfo info = CommsManager.GridService.RequestNeighbourInfo(regionID);

            if (info == null)
            {
                // can't find the region: Tell viewer and abort
                remoteClient.SendTeleportFailed("The teleport destination could not be found.");
                return;
            }

            lock (m_scenePresences)
            {
                if (m_scenePresences.ContainsKey(remoteClient.AgentId))
                {
                    m_sceneGridService.RequestTeleportToLocation(m_scenePresences[remoteClient.AgentId], info.RegionHandle,
                        position, Vector3.Zero, (uint)(TPFlags.SetLastToTarget | TPFlags.ViaLandmark));
                }
            }
        }

        /// <summary>
        /// Agent is crossing the border into a neighbouring region.  Tell the neighbour about it!
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="agentID"></param>
        /// <param name="position"></param>
        /// <param name="isFlying"></param>
        /// <returns></returns>
        public bool InformNeighbourOfCrossing(ulong regionHandle, UUID agentID, Vector3 position, bool isFlying)
        {
            return m_sceneGridService.CrossToNeighbouringRegion(regionHandle, agentID, position, isFlying);
        }

        public void SendOutChildAgentUpdates(ChildAgentDataUpdate cadu, ScenePresence presence)
        {
            m_sceneGridService.SendChildAgentDataUpdate(cadu, presence);
        }

        #endregion

        #region Module Methods

        /// <summary>
        ///
        /// </summary>
        /// <param name="name"></param>
        /// <param name="module"></param>
        public void AddModule(string name, IRegionModule module)
        {
            if (!Modules.ContainsKey(name))
            {
                Modules.Add(name, module);
            }
        }

        public void RegisterModuleCommander(string name, ICommander commander)
        {
            lock (m_moduleCommanders)
            {
                m_moduleCommanders.Add(name, commander);
            }
        }

        public ICommander GetCommander(string name)
        {
            lock (m_moduleCommanders)
            {
                return m_moduleCommanders[name];
            }
        }

        public Dictionary<string, ICommander> GetCommanders()
        {
            return m_moduleCommanders;
        }

        /// <summary>
        /// Register an interface to a region module.  This allows module methods to be called directly as
        /// well as via events.  If there is already a module registered for this interface, it is not replaced
        /// (is this the best behaviour?)
        /// </summary>
        /// <param name="mod"></param>
        public void RegisterModuleInterface<M>(M mod)
        {
            if (!ModuleInterfaces.ContainsKey(typeof(M)))
            {
                List<Object> l = new List<Object>();
                l.Add(mod);
                ModuleInterfaces.Add(typeof(M), l);
            }
        }

        public void StackModuleInterface<M>(M mod)
        {
            List<Object> l;
            if (ModuleInterfaces.ContainsKey(typeof(M)))
                l = ModuleInterfaces[typeof(M)];
            else
                l = new List<Object>();

            if (l.Contains(mod))
                return;

            l.Add(mod);
            ModuleInterfaces[typeof(M)] = l;
        }

        /// <summary>
        /// For the given interface, retrieve the region module which implements it.
        /// </summary>
        /// <returns>null if there is no module implementing that interface</returns>
        public override T RequestModuleInterface<T>()
        {
            if (ModuleInterfaces.ContainsKey(typeof(T)))
            {
                return (T)ModuleInterfaces[typeof(T)][0];
            }
            else
            {
                return default(T);
            }
        }

        public override T[] RequestModuleInterfaces<T>()
        {
            if (ModuleInterfaces.ContainsKey(typeof(T)))
            {
                List<T> ret = new List<T>();

                foreach (Object o in ModuleInterfaces[typeof(T)])
                    ret.Add((T)o);
                return ret.ToArray();
            }
            else
            {
                return new T[] { default(T) };
            }
        }

        public void SetObjectCapacity(int objects)
        {
            if (m_statsReporter != null)
            {
                m_statsReporter.SetObjectCapacity(objects);
            }
            objectCapacity = objects;
        }

        public List<FriendListItem> GetFriendList(UUID avatarID)
        {
            return CommsManager.GetUserFriendList(avatarID);
        }

        public Dictionary<UUID, FriendRegionInfo> GetFriendRegionInfos(List<UUID> uuids)
        {
            return CommsManager.GetFriendRegionInfos(uuids);
        }

        public List<UUID> InformFriendsInOtherRegion(UUID agentId, ulong destRegionHandle, List<UUID> friends, bool online)
        {
            return CommsManager.InformFriendsInOtherRegion(agentId, destRegionHandle, friends, online);
        }

        public bool TriggerTerminateFriend(ulong regionHandle, UUID agentID, UUID exFriendID)
        {
            return CommsManager.TriggerTerminateFriend(regionHandle, agentID, exFriendID);
        }

        #endregion

        #region Other Methods

        /// <summary>
        ///
        /// </summary>
        /// <param name="phase"></param>
        public void SetTimePhase(int phase)
        {
            m_timePhase = phase;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="avatarID"></param>
        /// <param name="objectName"></param>
        /// <param name="objectID"></param>
        /// <param name="ownerID"></param>
        /// <param name="groupOwned"></param>
        /// <param name="message"></param>
        /// <param name="url"></param>
        public void SendUrlToUser(UUID avatarID, string objectName, UUID objectID, UUID ownerID, bool groupOwned,
                                  string message, string url)
        {
            lock (m_scenePresences)
            {
                if (m_scenePresences.ContainsKey(avatarID))
                {
                    m_scenePresences[avatarID].ControllingClient.SendLoadURL(objectName, objectID, ownerID, groupOwned,
                                                                             message, url);
                }
            }
        }

        public void SendDialogToUser(UUID avatarID, string objectName, UUID objectID, UUID ownerID, string message, UUID TextureID, int ch, string[] buttonlabels)
        {
            lock (m_scenePresences)
            {
                if (m_scenePresences.ContainsKey(avatarID))
                {
                    m_scenePresences[avatarID].ControllingClient.SendDialog(
                        objectName, objectID, ownerID, message, TextureID, ch, buttonlabels);
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="url"></param>
        /// <param name="type"></param>
        /// <param name="body"></param>
        /// <returns></returns>
        public UUID MakeHttpRequest(string url, string type, string body)
        {
            if (m_httpRequestModule != null)
            {
                return m_httpRequestModule.MakeHttpRequest(url, type, body);
            }
            return UUID.Zero;
        }

        public virtual void StoreAddFriendship(UUID ownerID, UUID friendID, uint perms)
        {
            // TODO: m_sceneGridService.DoStuff;
            m_sceneGridService.AddNewUserFriend(ownerID, friendID, perms);
        }

        public virtual void StoreUpdateFriendship(UUID ownerID, UUID friendID, uint perms)
        {
            // TODO: m_sceneGridService.DoStuff;
            m_sceneGridService.UpdateUserFriendPerms(ownerID, friendID, perms);
        }

        public virtual void StoreRemoveFriendship(UUID ownerID, UUID ExfriendID)
        {
            // TODO: m_sceneGridService.DoStuff;
            m_sceneGridService.RemoveUserFriend(ownerID, ExfriendID);
        }

        public virtual List<FriendListItem> StoreGetFriendsForUser(UUID ownerID)
        {
            // TODO: m_sceneGridService.DoStuff;
            return m_sceneGridService.GetUserFriendList(ownerID);
        }

        public void AddPacketStats(int inPackets, int outPackets, int unAckedBytes)
        {
            m_statsReporter.AddInPackets(inPackets);
            m_statsReporter.AddOutPackets(outPackets);
            m_statsReporter.AddunAckedBytes(unAckedBytes);
        }

        public void AddAgentTime(int ms)
        {
            m_statsReporter.addFrameMS(ms);
            m_statsReporter.addAgentMS(ms);
        }

        public void AddAgentUpdates(int count)
        {
            m_statsReporter.AddAgentUpdates(count);
        }

        public void AddPendingDownloads(int count)
        {
            m_statsReporter.addPendingDownload(count);
        }

        #endregion

        #region Console Commands

        #region Alert Methods

        private void SendPermissionAlert(UUID user, string reason)
        {
            SendAlertToUser(user, reason, false);
        }

        /// <summary>
        /// Send an alert messages to all avatars in this scene.
        /// </summary>
        /// <param name="message"></param>
        public void SendGeneralAlert(string message)
        {
            List<ScenePresence> presenceList = GetScenePresences();

            foreach (ScenePresence presence in presenceList)
            {
                if (!presence.IsChildAgent)
                    presence.ControllingClient.SendAlertMessage(message);
            }
        }

        /// <summary>
        /// Send an alert message to a particular agent.
        /// </summary>
        /// <param name="agentID"></param>
        /// <param name="message"></param>
        /// <param name="modal"></param>
        public void SendAlertToUser(UUID agentID, string message, bool modal)
        {
            lock (m_scenePresences)
            {
                if (m_scenePresences.ContainsKey(agentID))
                {
                    m_scenePresences[agentID].ControllingClient.SendAgentAlertMessage(message, modal);
                }
            }
        }

        /// <summary>
        /// Handle a request for admin rights
        /// </summary>
        /// <param name="agentID"></param>
        /// <param name="sessionID"></param>
        /// <param name="token"></param>
        /// <param name="controllingClient"></param>
        public void handleRequestGodlikePowers(UUID agentID, UUID sessionID, UUID token, bool godLike,
                                               IClientAPI controllingClient)
        {
            lock (m_scenePresences)
            {
                // User needs to be logged into this sim
                if (m_scenePresences.ContainsKey(agentID))
                {
                    if (godLike == false)
                    {
                        m_scenePresences[agentID].GrantGodlikePowers(agentID, sessionID, token, godLike);
                        return;
                    }

                    // First check that this is the sim owner
                    if (ExternalChecks.ExternalChecksCanBeGodLike(agentID))
                    {
                        // Next we check for spoofing.....
                        UUID testSessionID = m_scenePresences[agentID].ControllingClient.SessionId;
                        if (sessionID == testSessionID)
                        {
                            if (sessionID == controllingClient.SessionId)
                            {
                                //m_log.Info("godlike: " + godLike.ToString());
                                m_scenePresences[agentID].GrantGodlikePowers(agentID, testSessionID, token, godLike);
                            }
                        }
                    }
                    else
                    {
                        m_scenePresences[agentID].ControllingClient.SendAgentAlertMessage("Request for god powers denied", false);
                    }
                }
            }
        }

        /// <summary>
        /// Sends a Big Blue Box message on the upper right of the screen to the client
        /// for all agents in the region
        /// </summary>
        /// <param name="FromAvatarID">The person sending the message</param>
        /// <param name="fromSessionID">The session of the person sending the message</param>
        /// <param name="FromAvatarName">The name of the person doing the sending</param>
        /// <param name="Message">The Message being sent to the user</param>
        public void SendRegionMessageFromEstateTools(UUID FromAvatarID, UUID fromSessionID, String FromAvatarName, String Message)
        {
            List<ScenePresence> presenceList = GetScenePresences();

            foreach (ScenePresence presence in presenceList)
            {
                if (!presence.IsChildAgent)
                    presence.ControllingClient.SendBlueBoxMessage(FromAvatarID, FromAvatarName, Message);
            }
        }

        /// <summary>
        /// Sends a Big Blue Box message on the upper right of the screen to the client
        /// for all agents in the estate
        /// </summary>
        /// <param name="FromAvatarID">The person sending the message</param>
        /// <param name="fromSessionID">The session of the person sending the message</param>
        /// <param name="FromAvatarName">The name of the person doing the sending</param>
        /// <param name="Message">The Message being sent to the user</param>
        public void SendEstateMessageFromEstateTools(UUID FromAvatarID, UUID fromSessionID, String FromAvatarName, String Message)
        {

            ClientManager.ForEachClient(delegate(IClientAPI controller)
                                        {
                                            controller.SendBlueBoxMessage(FromAvatarID, FromAvatarName, Message);
                                        }
                );
        }

        /// <summary>
        /// Kicks User specified from the simulator. This logs them off of the grid
        /// If the client gets the UUID: 44e87126e7944ded05b37c42da3d5cdb it assumes
        /// that you're kicking it even if the avatar's UUID isn't the UUID that the
        /// agent is assigned
        /// </summary>
        /// <param name="godID">The person doing the kicking</param>
        /// <param name="sessionID">The session of the person doing the kicking</param>
        /// <param name="agentID">the person that is being kicked</param>
        /// <param name="kickflags">This isn't used apparently</param>
        /// <param name="reason">The message to send to the user after it's been turned into a field</param>
        public void HandleGodlikeKickUser(UUID godID, UUID sessionID, UUID agentID, uint kickflags, byte[] reason)
        {
            // For some reason the client sends this seemingly hard coded UUID for kicking everyone.   Dun-know.
            UUID kickUserID = new UUID("44e87126e7944ded05b37c42da3d5cdb");
            lock (m_scenePresences)
            {
                if (m_scenePresences.ContainsKey(agentID) || agentID == kickUserID)
                {
                    if (ExternalChecks.ExternalChecksCanBeGodLike(godID))
                    {
                        if (agentID == kickUserID)
                        {
                            ClientManager.ForEachClient(delegate(IClientAPI controller)
                                                        {
                                                            if (controller.AgentId != godID)
                                                                controller.Kick(Utils.BytesToString(reason));
                                                        }
                                );

                            // This is a bit crude.   It seems the client will be null before it actually stops the thread
                            // The thread will kill itself eventually :/
                            // Is there another way to make sure *all* clients get this 'inter region' message?
                            ClientManager.ForEachClient(delegate(IClientAPI controller)
                                                        {
                                                            ScenePresence p = GetScenePresence(controller.AgentId);
                                                            bool childagent = p != null && p.IsChildAgent;
                                                            if (controller.AgentId != godID && !childagent)
                                                                // Do we really want to kick the initiator of this madness?
                                                            {
                                                                controller.Close(true);
                                                            }
                                                        }
                                );
                        }
                        else
                        {
                            m_sceneGraph.removeUserCount(!m_scenePresences[agentID].IsChildAgent);

                            m_scenePresences[agentID].ControllingClient.Kick(Utils.BytesToString(reason));
                            m_scenePresences[agentID].ControllingClient.Close(true);
                        }
                    }
                    else
                    {
                        if (m_scenePresences.ContainsKey(godID))
                            m_scenePresences[godID].ControllingClient.SendAgentAlertMessage("Kick request denied", false);
                    }
                }
            }
        }

        public void HandleObjectPermissionsUpdate(IClientAPI controller, UUID agentID, UUID sessionID, byte field, uint localId, uint mask, byte set)
        {
            // Check for spoofing..  since this is permissions we're talking about here!
            if ((controller.SessionId == sessionID) && (controller.AgentId == agentID))
            {
                // Tell the object to do permission update
                if (localId != 0)
                {
                    SceneObjectGroup chObjectGroup = GetGroupByPrim(localId);
                    if (chObjectGroup != null)
                    {
                        chObjectGroup.UpdatePermissions(agentID, field, localId, mask, set);
                    }
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="firstName"></param>
        /// <param name="lastName"></param>
        /// <param name="message"></param>
        /// <param name="modal"></param>
        public void SendAlertToUser(string firstName, string lastName, string message, bool modal)
        {
            List<ScenePresence> presenceList = GetScenePresences();

            foreach (ScenePresence presence in presenceList)
            {
                if (presence.Firstname == firstName && presence.Lastname == lastName)
                {
                    presence.ControllingClient.SendAgentAlertMessage(message, modal);
                    break;
                }
            }
        }

        /// <summary>
        /// Handle an alert command from the console.
        /// FIXME: Command parsing code really shouldn't be in this core Scene class.
        /// </summary>
        /// <param name="commandParams"></param>
        public void HandleAlertCommand(string[] commandParams)
        {
            if (commandParams[0] == "general")
            {
                string message = CombineParams(commandParams, 1);
                SendGeneralAlert(message);
            }
            else
            {
                string message = CombineParams(commandParams, 2);
                SendAlertToUser(commandParams[0], commandParams[1], message, false);
            }
        }

        private string CombineParams(string[] commandParams, int pos)
        {
            string result = String.Empty;
            for (int i = pos; i < commandParams.Length; i++)
            {
                result += commandParams[i] + " ";
            }
            return result;
        }

        #endregion

        /// <summary>
        /// Causes all clients to get a full object update on all of the objects in the scene.
        /// </summary>
        public void ForceClientUpdate()
        {
            List<EntityBase> EntityList = GetEntities();

            foreach (EntityBase ent in EntityList)
            {
                if (ent is SceneObjectGroup)
                {
                    ((SceneObjectGroup)ent).ScheduleGroupForFullUpdate();
                }
            }
        }

        /// <summary>
        /// This is currently only used for scale (to scale to MegaPrim size)
        /// There is a console command that calls this in OpenSimMain
        /// </summary>
        /// <param name="cmdparams"></param>
        public void HandleEditCommand(string[] cmdparams)
        {
            Console.WriteLine("Searching for Primitive: '" + cmdparams[0] + "'");

            List<EntityBase> EntityList = GetEntities();

            foreach (EntityBase ent in EntityList)
            {
                if (ent is SceneObjectGroup)
                {
                    SceneObjectPart part = ((SceneObjectGroup)ent).GetChildPart(((SceneObjectGroup)ent).UUID);
                    if (part != null)
                    {
                        if (part.Name == cmdparams[0])
                        {
                            part.Resize(
                                new Vector3(Convert.ToSingle(cmdparams[1]), Convert.ToSingle(cmdparams[2]),
                                              Convert.ToSingle(cmdparams[3])));

                            Console.WriteLine("Edited scale of Primitive: " + part.Name);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Shows various details about the sim based on the parameters supplied by the console command in openSimMain.
        /// </summary>
        /// <param name="showParams">What to show</param>
        public void Show(string[] showParams)
        {
            switch (showParams[0])
            {
                case "users":
                    m_log.Error("Current Region: " + RegionInfo.RegionName);
                    m_log.ErrorFormat("{0,-16}{1,-16}{2,-25}{3,-25}{4,-16}{5,-16}{6,-16}", "Firstname", "Lastname",
                                      "Agent ID", "Session ID", "Circuit", "IP", "World");

                    foreach (ScenePresence scenePresence in GetAvatars())
                    {
                        m_log.ErrorFormat("{0,-16}{1,-16}{2,-25}{3,-25}{4,-16},{5,-16}{6,-16}",
                                          scenePresence.Firstname,
                                          scenePresence.Lastname,
                                          scenePresence.UUID,
                                          scenePresence.ControllingClient.AgentId,
                                          "Unknown",
                                          "Unknown",
                                          RegionInfo.RegionName);
                    }
                    break;
                case "modules":
                    m_log.Error("The currently loaded modules in " + RegionInfo.RegionName + " are:");
                    foreach (IRegionModule module in Modules.Values)
                    {
                        if (!module.IsSharedModule)
                        {
                            m_log.Error("Region Module: " + module.Name);
                        }
                    }
                    break;
            }
        }

        #endregion

        #region Script Handling Methods

        /// <summary>
        /// Console command handler to send script command to script engine.
        /// </summary>
        /// <param name="args"></param>
        public void SendCommandToPlugins(string[] args)
        {
            m_eventManager.TriggerOnPluginConsole(args);
        }

        public double GetLandHeight(int x, int y)
        {
            return Heightmap[x, y];
        }

        public UUID GetLandOwner(float x, float y)
        {
            ILandObject land = LandChannel.GetLandObject(x, y);
            if (land == null)
            {
                return UUID.Zero;
            }
            else
            {
                return land.landData.OwnerID;
            }
        }

        public LandData GetLandData(float x, float y)
        {
            return LandChannel.GetLandObject(x, y).landData;
        }

        public LandData GetLandData(uint x, uint y)
        {
            m_log.DebugFormat("[SCENE] returning land for {0},{1}", x, y);
            return LandChannel.GetLandObject((int)x, (int)y).landData;
        }

        public void SetLandMusicURL(float x, float y, string url)
        {
            ILandObject land = LandChannel.GetLandObject(x, y);
            if (land == null)
            {
                return;
            }
            else
            {
                land.landData.MusicURL = url;
                land.sendLandUpdateToAvatarsOverMe();
                return;
            }
        }

        public void SetLandMediaURL(float x, float y, string url)
        {
            ILandObject land = LandChannel.GetLandObject(x, y);

            if (land == null)
            {
                return;
            }

            else
            {
                land.landData.MediaURL = url;
                land.sendLandUpdateToAvatarsOverMe();
                return;
            }
        }

        public RegionInfo RequestClosestRegion(string name)
        {
            return m_sceneGridService.RequestClosestRegion(name);
        }

        #endregion

        #region Script Engine

        private List<ScriptEngineInterface> ScriptEngines = new List<ScriptEngineInterface>();
        private bool m_dumpAssetsToFile;

        /// <summary>
        ///
        /// </summary>
        /// <param name="scriptEngine"></param>
        public void AddScriptEngine(ScriptEngineInterface scriptEngine)
        {
            ScriptEngines.Add(scriptEngine);
            scriptEngine.InitializeEngine(this);
        }

        public void TriggerObjectChanged(uint localID, uint change)
        {
            m_eventManager.TriggerOnScriptChangedEvent(localID, change);
        }

        public void TriggerAtTargetEvent(uint localID, uint handle, Vector3 targetpos, Vector3 currentpos)
        {
            m_eventManager.TriggerAtTargetEvent(localID, handle, targetpos, currentpos);
        }

        public void TriggerNotAtTargetEvent(uint localID)
        {
            m_eventManager.TriggerNotAtTargetEvent(localID);
        }

        private bool ScriptDanger(SceneObjectPart part,Vector3 pos)
        {
            ILandObject parcel = LandChannel.GetLandObject(pos.X, pos.Y);
            if (part != null)
            {
                if (parcel != null)
                {
                    if ((parcel.landData.Flags & (uint)Parcel.ParcelFlags.AllowOtherScripts) != 0)
                    {
                        return true;
                    }
                    else if ((parcel.landData.Flags & (uint)Parcel.ParcelFlags.AllowGroupScripts) != 0)
                    {
                        if (part.OwnerID == parcel.landData.OwnerID || (parcel.landData.IsGroupOwned && part.GroupID == parcel.landData.GroupID) || ExternalChecks.ExternalChecksCanBeGodLike(part.OwnerID))
                        {
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }
                    else
                    {
                        if (part.OwnerID == parcel.landData.OwnerID)
                        {
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }
                }
                else
                {

                    if (pos.X > 0f && pos.X < Constants.RegionSize && pos.Y > 0f && pos.Y < Constants.RegionSize)
                    {
                        // The only time parcel != null when an object is inside a region is when
                        // there is nothing behind the landchannel.  IE, no land plugin loaded.
                        return true;
                    }
                    else
                    {
                        // The object is outside of this region.  Stop piping events to it.
                        return false;
                    }
                }
            }
            else
            {
                return false;
            }
        }

        public bool ScriptDanger(uint localID, Vector3 pos)
        {
            SceneObjectPart part = GetSceneObjectPart(localID);
            if (part != null)
            {
                return ScriptDanger(part, pos);
            }
            else
            {
                return false;
            }
        }

        public bool PipeEventsForScript(uint localID)
        {
            SceneObjectPart part = GetSceneObjectPart(localID);
            if (part != null)
            {
                // Changed so that child prims of attachments return ScriptDanger for their parent, so that
                //  their scripts will actually run.
                //      -- Leaf, Tue Aug 12 14:17:05 EDT 2008
                SceneObjectPart parent = part.ParentGroup.RootPart;
                if (parent != null && parent.IsAttachment)
                    return ScriptDanger(parent, parent.GetWorldPosition());
                else
                    return ScriptDanger(part, part.GetWorldPosition());
            }
            else
            {
                return false;
            }
        }

        #endregion

        #region SceneGraph wrapper methods

        /// <summary>
        ///
        /// </summary>
        /// <param name="localID"></param>
        /// <returns></returns>
        public UUID ConvertLocalIDToFullID(uint localID)
        {
            return m_sceneGraph.ConvertLocalIDToFullID(localID);
        }

        public void SwapRootAgentCount(bool rootChildChildRootTF)
        {
            m_sceneGraph.SwapRootChildAgent(rootChildChildRootTF);
        }

        public void AddPhysicalPrim(int num)
        {
            m_sceneGraph.AddPhysicalPrim(num);
        }

        public void RemovePhysicalPrim(int num)
        {
            m_sceneGraph.RemovePhysicalPrim(num);
        }

        //The idea is to have a group of method that return a list of avatars meeting some requirement
        // ie it could be all m_scenePresences within a certain range of the calling prim/avatar.

        /// <summary>
        /// Return a list of all avatars in this region.
        /// This list is a new object, so it can be iterated over without locking.
        /// </summary>
        /// <returns></returns>
        public List<ScenePresence> GetAvatars()
        {
            return m_sceneGraph.GetAvatars();
        }

        /// <summary>
        /// Return a list of all ScenePresences in this region.  This returns child agents as well as root agents.
        /// This list is a new object, so it can be iterated over without locking.
        /// </summary>
        /// <returns></returns>
        public List<ScenePresence> GetScenePresences()
        {
            return m_sceneGraph.GetScenePresences();
        }

        /// <summary>
        /// Request a filtered list of ScenePresences in this region.
        /// This list is a new object, so it can be iterated over without locking.
        /// </summary>
        /// <param name="filter"></param>
        /// <returns></returns>
        public List<ScenePresence> GetScenePresences(FilterAvatarList filter)
        {
            return m_sceneGraph.GetScenePresences(filter);
        }

        /// <summary>
        /// Request a scene presence by UUID
        /// </summary>
        /// <param name="avatarID"></param>
        /// <returns></returns>
        public ScenePresence GetScenePresence(UUID avatarID)
        {
            return m_sceneGraph.GetScenePresence(avatarID);
        }

        /// <summary>
        /// Request an Avatar's Child Status - used by ClientView when a 'kick everyone' or 'estate message' occurs
        /// </summary>
        /// <param name="avatarID">AvatarID to lookup</param>
        /// <returns></returns>
        public override bool PresenceChildStatus(UUID avatarID)
        {
            ScenePresence cp = GetScenePresence(avatarID);
            return cp.IsChildAgent;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="action"></param>
        public void ForEachScenePresence(Action<ScenePresence> action)
        {
            // We don't want to try to send messages if there are no avatars.
            if (m_scenePresences != null)
            {
                try
                {
                    List<ScenePresence> presenceList = GetScenePresences();
                    foreach (ScenePresence presence in presenceList)
                    {
                        action(presence);
                    }
                }
                catch (Exception e)
                {
                    m_log.Info("[BUG]: " + e.ToString());
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="action"></param>
        //        public void ForEachObject(Action<SceneObjectGroup> action)
        //        {
        //            List<SceneObjectGroup> presenceList;
        //
        //            lock (m_sceneObjects)
        //            {
        //                presenceList = new List<SceneObjectGroup>(m_sceneObjects.Values);
        //            }
        //
        //            foreach (SceneObjectGroup presence in presenceList)
        //            {
        //                action(presence);
        //            }
        //        }

        /// <summary>
        /// Get a named prim contained in this scene (will return the first
        /// found, if there are more than one prim with the same name)
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public SceneObjectPart GetSceneObjectPart(string name)
        {
            return m_sceneGraph.GetSceneObjectPart(name);
        }

        /// <summary>
        /// Get a prim via its local id
        /// </summary>
        /// <param name="localID"></param>
        /// <returns></returns>
        public SceneObjectPart GetSceneObjectPart(uint localID)
        {
            return m_sceneGraph.GetSceneObjectPart(localID);
        }

        /// <summary>
        /// Get a prim via its UUID
        /// </summary>
        /// <param name="fullID"></param>
        /// <returns></returns>
        public SceneObjectPart GetSceneObjectPart(UUID fullID)
        {
            return m_sceneGraph.GetSceneObjectPart(fullID);
        }

        internal bool TryGetAvatar(UUID avatarId, out ScenePresence avatar)
        {
            return m_sceneGraph.TryGetAvatar(avatarId, out avatar);
        }

        internal bool TryGetAvatarByName(string avatarName, out ScenePresence avatar)
        {
            return m_sceneGraph.TryGetAvatarByName(avatarName, out avatar);
        }

        internal void ForEachClient(Action<IClientAPI> action)
        {
            m_sceneGraph.ForEachClient(action);
        }

        /// <summary>
        /// Returns a list of the entities in the scene.  This is a new list so operations perform on the list itself
        /// will not affect the original list of objects in the scene.
        /// </summary>
        /// <returns></returns>
        public List<EntityBase> GetEntities()
        {
            return m_sceneGraph.GetEntities();
        }

        #endregion

        #region BaseHTTPServer wrapper methods

        public bool AddHTTPHandler(string method, GenericHTTPMethod handler)
        {
            return m_httpListener.AddHTTPHandler(method, handler);
        }

        public bool AddXmlRPCHandler(string method, XmlRpcMethod handler)
        {
            return m_httpListener.AddXmlRPCHandler(method, handler);
        }

        public void AddStreamHandler(IRequestHandler handler)
        {
            m_httpListener.AddStreamHandler(handler);
        }

        public bool AddLLSDHandler(string path, LLSDMethod handler)
        {
           return m_httpListener.AddLLSDHandler(path, handler);
        }

        public void RemoveStreamHandler(string httpMethod, string path)
        {
            m_httpListener.RemoveStreamHandler(httpMethod, path);
        }

        public void RemoveHTTPHandler(string httpMethod, string path)
        {
            m_httpListener.RemoveHTTPHandler(httpMethod, path);
        }

        public bool RemoveLLSDHandler(string path, LLSDMethod handler)
        {
           return m_httpListener.RemoveLLSDHandler(path, handler);
        }

        #endregion

        #region Avatar Appearance Default

        public static void GetDefaultAvatarAppearance(out AvatarWearable[] wearables, out byte[] visualParams)
        {
            visualParams = GetDefaultVisualParams();
            wearables = AvatarWearable.DefaultWearables;
        }

        private static byte[] GetDefaultVisualParams()
        {
            byte[] visualParams;
            visualParams = new byte[218];
            for (int i = 0; i < 218; i++)
            {
                visualParams[i] = 100;
            }
            return visualParams;
        }

        #endregion

        public void ParcelMediaSetTime(float time)
        {
            //should be doing this by parcel, but as its only for testing
            // The use of Thread.Sleep here causes the following compiler error under mono 1.2.4
            // OpenSim/Region/Environment/Scenes/Scene.cs(3675,17): error CS0103: The name `Thread' does not exist
            // in the context of `<>c__CompilerGenerated17'
            // MW said it was okay to comment the body of this method out for now since the code is experimental
            // and will be replaced anyway
//            ForEachClient(delegate(IClientAPI client)
//            {
//                client.SendParcelMediaCommand((uint)(2), ParcelMediaCommandEnum.Pause, 0);
//                Thread.Sleep(10);
//                client.SendParcelMediaCommand((uint)(64), ParcelMediaCommandEnum.Time, time);
//                Thread.Sleep(200);
//                client.SendParcelMediaCommand((uint)(4), ParcelMediaCommandEnum.Play, 0);
//            });
        }

        public void RegionHandleRequest(IClientAPI client, UUID regionID)
        {
            RegionInfo info;
            if (regionID == RegionInfo.RegionID)
                info = RegionInfo;
            else
                info = CommsManager.GridService.RequestNeighbourInfo(regionID);

            if (info != null)
                client.SendRegionHandle(regionID, info.RegionHandle);
        }

        public void TerrainUnAcked(IClientAPI client, int patchX, int patchY)
        {
            //Console.WriteLine("Terrain packet unacked, resending patch: " + patchX + " , " + patchY);
             client.SendLayerData(patchX, patchY, Heightmap.GetFloatsSerialised());
        }

        public void SetRootAgentScene(UUID agentID)
        {
            IInventoryTransferModule inv = RequestModuleInterface<IInventoryTransferModule>();
            if (inv == null)
                return;

            inv.SetRootAgentScene(agentID, this);
        }

        public bool NeedSceneCacheClear(UUID agentID)
        {
            IInventoryTransferModule inv = RequestModuleInterface<IInventoryTransferModule>();
            if (inv == null)
                return true;

            return inv.NeedSceneCacheClear(agentID, this);
        }

        public void ObjectSaleInfo(IClientAPI client, UUID agentID, UUID sessionID, uint localID, byte saleType, int salePrice)
        {
            SceneObjectPart part = GetSceneObjectPart(localID);
            if (part == null || part.ParentGroup == null)
                return;

            if (part.ParentGroup.IsDeleted)
                return;

            part = part.ParentGroup.RootPart;

            part.ObjectSaleType = saleType;
            part.SalePrice = salePrice;

            part.ParentGroup.HasGroupChanged = true;

            part.GetProperties(client);
        }

        public bool PerformObjectBuy(IClientAPI remoteClient, UUID categoryID,
                uint localID, byte saleType)
        {
            SceneObjectPart part = GetSceneObjectPart(localID);

            if (part == null)
                return false;

            if (part.ParentGroup == null)
                return false;

            SceneObjectGroup group = part.ParentGroup;

            switch (saleType)
            {
            case 1: // Sell as original (in-place sale)
                uint effectivePerms=group.GetEffectivePermissions();

                if ((effectivePerms & (uint)PermissionMask.Transfer) == 0)
                {
                    remoteClient.SendAgentAlertMessage("This item doesn't appear to be for sale", false);
                    return false;
                }

                group.SetOwnerId(remoteClient.AgentId);
                group.SetRootPartOwner(part, remoteClient.AgentId,
                        remoteClient.ActiveGroupId);

                List<SceneObjectPart> partList =
                    new List<SceneObjectPart>(group.Children.Values);

                if (ExternalChecks.ExternalChecksPropagatePermissions())
                {
                    foreach (SceneObjectPart child in partList)
                    {
                        child.ChangeInventoryOwner(remoteClient.AgentId);
                        child.ApplyNextOwnerPermissions();
                    }
                }

                part.ObjectSaleType = 0;
                part.SalePrice = 10;

                group.HasGroupChanged = true;
                part.GetProperties(remoteClient);
                part.ScheduleFullUpdate();

                break;

            case 2: // Sell a copy
                string sceneObjectXml = group.ToXmlString();

                CachedUserInfo userInfo =
                    CommsManager.UserProfileCacheService.GetUserDetails(remoteClient.AgentId);

                if (userInfo != null)
                {
                    uint perms=group.GetEffectivePermissions();

                    if ((perms & (uint)PermissionMask.Transfer) == 0)
                    {
                        remoteClient.SendAgentAlertMessage("This item doesn't appear to be for sale", false);
                        return false;
                    }

                    AssetBase asset = CreateAsset(
                        group.GetPartName(localID),
                        group.GetPartDescription(localID),
                        (sbyte)AssetType.Object,
                        Utils.StringToBytes(sceneObjectXml));
                    AssetCache.AddAsset(asset);

                    InventoryItemBase item = new InventoryItemBase();
                    item.Creator = part.CreatorID;

                    item.ID = UUID.Random();
                    item.Owner = remoteClient.AgentId;
                    item.AssetID = asset.FullID;
                    item.Description = asset.Description;
                    item.Name = asset.Name;
                    item.AssetType = asset.Type;
                    item.InvType = (int)InventoryType.Object;
                    item.Folder = categoryID;

                    uint nextPerms=(perms & 7) << 13;
                    if ((nextPerms & (uint)PermissionMask.Copy) == 0)
                        perms &= ~(uint)PermissionMask.Copy;
                    if ((nextPerms & (uint)PermissionMask.Transfer) == 0)
                        perms &= ~(uint)PermissionMask.Transfer;
                    if ((nextPerms & (uint)PermissionMask.Modify) == 0)
                        perms &= ~(uint)PermissionMask.Modify;

                    item.BasePermissions = perms & part.NextOwnerMask;
                    item.CurrentPermissions = perms & part.NextOwnerMask;
                    item.NextPermissions = part.NextOwnerMask;
                    item.EveryOnePermissions = part.EveryoneMask &
                                               part.NextOwnerMask;
                    item.GroupPermissions = part.GroupMask &
                                               part.NextOwnerMask;
                    item.CurrentPermissions |= 8; // Slam!
                    item.CreationDate = Util.UnixTimeSinceEpoch();

                    userInfo.AddItem(item);
                    remoteClient.SendInventoryItemCreateUpdate(item);
                }
                else
                {
                    remoteClient.SendAgentAlertMessage("Cannot buy now. Your inventory is unavailable", false);
                    return false;
                }
                break;

            case 3: // Sell contents
                List<UUID> invList = part.GetInventoryList();

                bool okToSell = true;

                foreach (UUID invID in invList)
                {
                    TaskInventoryItem item = part.GetInventoryItem(invID);
                    if ((item.CurrentPermissions &
                            (uint)PermissionMask.Transfer) == 0)
                    {
                        okToSell = false;
                        break;
                    }
                }

                if (!okToSell)
                {
                    remoteClient.SendAgentAlertMessage("This item's inventory doesn't appear to be for sale", false);
                    return false;
                }

                if (invList.Count > 0)
                    MoveTaskInventoryItems(remoteClient.AgentId, part.Name,
                            part, invList);
                break;
            }

            return true;
        }

        public void CleanTempObjects()
        {
            List<EntityBase> objs = GetEntities();

            foreach (EntityBase obj in objs)
            {
                if (obj is SceneObjectGroup)
                {
                    SceneObjectGroup grp = (SceneObjectGroup)obj;

                    if (!grp.IsDeleted)
                    {
                        if ((grp.RootPart.Flags & PrimFlags.TemporaryOnRez) != 0)
                        {
                            if (grp.RootPart.Expires <= DateTime.Now)
                                DeleteSceneObject(grp, false);
                        }
                    }
                }
            }
        }

        public void DeleteFromStorage(UUID uuid)
        {
            m_storageManager.DataStore.RemoveObject(uuid, m_regInfo.RegionID);
        }

        public int GetHealth()
        {
            int health=1; // Start at 1, means we're up

            // A login in the last 4 mins? We can't be doing too badly
            //
            if ((System.Environment.TickCount - m_LastLogin) < 240000)
                health++;

            return 0;
        }
    }
}
