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
using System.Threading;
using System.Timers;
using Axiom.Math;
using libsecondlife;
using libsecondlife.Packets;
using OpenJPEGNet;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Servers;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Modules.World.Archiver;
using OpenSim.Region.Environment.Modules.World.Serialiser;
using OpenSim.Region.Environment.Modules.World.Terrain;
using OpenSim.Region.Environment.Scenes.Scripting;
using OpenSim.Region.Physics.Manager;
using Nini.Config;
using Caps=OpenSim.Framework.Communications.Capabilities.Caps;
using Image=System.Drawing.Image;
using Timer=System.Timers.Timer;

namespace OpenSim.Region.Environment.Scenes
{
    public delegate bool FilterAvatarList(ScenePresence avatar);

    public partial class Scene : SceneBase
    {
        public delegate void SynchronizeSceneHandler(Scene scene);
        public SynchronizeSceneHandler SynchronizeScene = null;
        public int splitID = 0;

        #region Fields

        protected Timer m_heartbeatTimer = new Timer();
        protected Timer m_restartWaitTimer = new Timer();

        protected SimStatsReporter m_statsReporter;

        protected List<RegionInfo> m_regionRestartNotifyList = new List<RegionInfo>();
        protected List<RegionInfo> m_neighbours = new List<RegionInfo>();

        public InnerScene m_innerScene;

        /// <summary>
        /// The last allocated local prim id.  When a new local id is requested, the next number in the sequence is 
        /// dispenced.
        /// </summary>       
        private uint m_lastAllocatedLocalId = 720000;
        
        private readonly Mutex _primAllocateMutex = new Mutex(false);

        private int m_timePhase = 24;

        private readonly Mutex updateLock;

        /// <summary>
        /// Are we applying physics to any of the prims in this scene?
        /// </summary>
        public bool m_physicalPrim;
        public float m_maxNonphys = 65536;
        public float m_maxPhys = 10;

        public bool m_seeIntoRegionFromNeighbor;
        public int MaxUndoCount = 5;
        private int m_RestartTimerCounter;
        private readonly Timer m_restartTimer = new Timer(15000); // Wait before firing
        private int m_incrementsof15seconds = 0;
        private volatile bool m_backingup = false;

        protected string m_simulatorVersion = "unknown";

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
        protected Dictionary<LLUUID, Caps> m_capsHandlers = new Dictionary<LLUUID, Caps>();

        protected BaseHttpServer m_httpListener;

        protected Dictionary<string, IRegionModule> m_modules = new Dictionary<string, IRegionModule>();
        public Dictionary<string, IRegionModule> Modules
        {
            get { return m_modules; }
        }
        protected Dictionary<Type, object> ModuleInterfaces = new Dictionary<Type, object>();
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

        #endregion

        #region Properties

        public AgentCircuitManager AuthenticateHandler
        {
            get { return m_authenticateHandler; }
        }

        // an instance to the physics plugin's Scene object.
        public PhysicsScene PhysicsScene
        {
            set { m_innerScene.PhysicsScene = value; }
            get { return (m_innerScene.PhysicsScene); }
        }

        // This gets locked so things stay thread safe.
        public object SyncRoot
        {
            get { return m_innerScene.m_syncRoot; }
        }

        public float TimeDilation
        {
            get { return m_timedilation; }
        }

        public int TimePhase
        {
            get { return m_timePhase; }
        }

        // Local reference to the objects in the scene (which are held in innerScene)
        //        public Dictionary<LLUUID, SceneObjectGroup> Objects
        //        {
        //            get { return m_innerScene.SceneObjects; }
        //        }

        // Reference to all of the agents in the scene (root and child)
        protected Dictionary<LLUUID, ScenePresence> m_scenePresences
        {
            get { return m_innerScene.ScenePresences; }
            set { m_innerScene.ScenePresences = value; }
        }

        //        protected Dictionary<LLUUID, SceneObjectGroup> m_sceneObjects
        //        {
        //            get { return m_innerScene.SceneObjects; }
        //            set { m_innerScene.SceneObjects = value; }
        //        }

        /// <summary>
        /// The dictionary of all entities in this scene.  The contents of this dictionary may be changed at any time.
        /// If you wish to add or remove entities, please use the appropriate method for that entity rather than
        /// editing this dictionary directly.
        ///
        /// If you want a list of entities where the list itself is guaranteed not to change, please use
        /// GetEntities()
        /// </summary>
        public Dictionary<LLUUID, EntityBase> Entities
        {
            get { return m_innerScene.Entities; }
            set { m_innerScene.Entities = value; }
        }

        public Dictionary<LLUUID, ScenePresence> m_restorePresences
        {
            get { return m_innerScene.RestorePresences; }
            set { m_innerScene.RestorePresences = value; }
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
            updateLock = new Mutex(false);
            m_moduleLoader = moduleLoader;
            m_authenticateHandler = authen;
            CommsManager = commsMan;
            m_sceneGridService = sceneGridService;
            m_sceneGridService.debugRegionName = regInfo.RegionName;
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

            // Load region settings
            m_regInfo.RegionSettings = m_storageManager.DataStore.LoadRegionSettings(m_regInfo.RegionID);
            if (m_storageManager.EstateDataStore != null)
                m_regInfo.EstateSettings = m_storageManager.EstateDataStore.LoadEstateSettings(m_regInfo.RegionID);



            //Bind Storage Manager functions to some land manager functions for this scene
            EventManager.OnLandObjectAdded +=
                new EventManager.LandObjectAdded(m_storageManager.DataStore.StoreLandObject);
            EventManager.OnLandObjectRemoved +=
                new EventManager.LandObjectRemoved(m_storageManager.DataStore.RemoveLandObject);

            m_innerScene = new InnerScene(this, m_regInfo);

            // If the Inner scene has an Unrecoverable error, restart this sim.
            // Currently the only thing that causes it to happen is two kinds of specific
            // Physics based crashes.
            //
            // Out of memory
            // Operating system has killed the plugin
            m_innerScene.UnRecoverableError += RestartNow;

            RegisterDefaultSceneEvents();

            m_httpListener = httpServer;
            m_dumpAssetsToFile = dumpAssetsToFile;

            m_scripts_enabled = !RegionInfo.RegionSettings.DisableScripts;

            m_physics_enabled = !RegionInfo.RegionSettings.DisablePhysics;

            m_statsReporter = new SimStatsReporter(this);
            m_statsReporter.OnSendStatsResult += SendSimStatsPackets;

            m_statsReporter.SetObjectCapacity(objectCapacity);

            m_simulatorVersion = simulatorVersion
                + " ChilTasks:" + m_seeIntoRegionFromNeighbor.ToString()
                + " PhysPrim:" + m_physicalPrim.ToString();

            try
            {
                IConfig startupConfig = m_config.Configs["Startup"];
                m_maxNonphys = startupConfig.GetFloat("NonPhysicalPrimMax", 65536.0f);
                m_maxPhys = startupConfig.GetFloat("PhysicalPrimMax", 10.0f);
            }
            catch (Exception)
            {
                m_log.Warn("Failed to load StartupConfig");
            }

        }

        #endregion

        #region Startup / Close Methods

        protected virtual void RegisterDefaultSceneEvents()
        {
            m_eventManager.OnPermissionError += SendPermissionAlert;
        }

        public override string GetSimulatorVersion()
        {
            return m_simulatorVersion;
        }

        public override bool OtherRegionUp(RegionInfo otherRegion)
        {
            // Another region is up.
            // Gets called from Grid Comms (SceneCommunicationService<---RegionListener<----LocalBackEnd<----OGS1)
            // We have to tell all our ScenePresences about it..
            // and add it to the neighbor list.

            // We only add it to the neighbor list if it's within 1 region from here.
            // Agents may have draw distance values that cross two regions though, so
            // we add it to the notify list regardless of distance.
            // We'll check the agent's draw distance before notifying them though.


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
                if ((resultX <= 1) &&
                    (resultY <= 1))
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

        // Given float seconds, this will restart the region.
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
                SendRegionMessageFromEstateTools(LLUUID.Random(), LLUUID.Random(), String.Empty, RegionInfo.RegionName + ": Restarting in 2 Minutes");
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
                    SendRegionMessageFromEstateTools(LLUUID.Random(), LLUUID.Random(), String.Empty, RegionInfo.RegionName + ": Restarting in " +
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
                                ((SceneObjectGroup)ent).CreateScriptInstances(0, false);
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
                return m_neighbours.Count;
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
            m_heartbeatTimer.Close();
            // close the inner scene
            m_innerScene.Close();
            // De-register with region communications (events cleanup)
            UnRegisterReginWithComms();

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
            m_log.Debug("[SCENE]: Starting timer");
            m_heartbeatTimer.Enabled = true;
            m_heartbeatTimer.Interval = (int)(m_timespan * 1000);
            m_heartbeatTimer.Elapsed += new ElapsedEventHandler(Heartbeat);
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
        private void Heartbeat(object sender, EventArgs e)
        {
            Update();
        }

        /// <summary>
        /// Performs per-frame updates on the scene, this should be the central scene loop
        /// </summary>
        public override void Update()
        {
            TimeSpan SinceLastFrame = DateTime.Now - m_lastupdate;
            // Aquire a lock so only one update call happens at once
            updateLock.WaitOne();
            float physicsFPS = 0;
            //m_log.Info("sadfadf" + m_neighbours.Count.ToString());
            int agentsInScene = m_innerScene.GetRootAgentCount() + m_innerScene.GetChildAgentCount();

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
                    m_innerScene.UpdatePreparePhysics();
                physicsMS2 = System.Environment.TickCount - physicsMS2;

                if (m_frame % m_update_entitymovement == 0)
                    m_innerScene.UpdateEntityMovement();

                physicsMS = System.Environment.TickCount;
                if ((m_frame % m_update_physics == 0) && m_physics_enabled)
                    physicsFPS = m_innerScene.UpdatePhysics(
                        Math.Max(SinceLastFrame.TotalSeconds, m_timespan)
                        );
                if (m_frame % m_update_physics == 0 && SynchronizeScene != null)
                    SynchronizeScene(this);

                physicsMS = System.Environment.TickCount - physicsMS;
                physicsMS += physicsMS2;

                otherMS = System.Environment.TickCount;
                // run through all entities looking for updates (slow)
                if (m_frame % m_update_entities == 0)
                    m_innerScene.UpdateEntities();

                // run through entities that have scheduled themselves for
                // updates looking for updates(faster)
                if (m_frame % m_update_entitiesquick == 0)
                    m_innerScene.ProcessUpdates();

                // Run through scenepresences looking for updates
                if (m_frame % m_update_presences == 0)
                    m_innerScene.UpdatePresences();

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
                    m_statsReporter.SetRootAgents(m_innerScene.GetRootAgentCount());
                    m_statsReporter.SetChildAgents(m_innerScene.GetChildAgentCount());
                    m_statsReporter.SetObjects(m_innerScene.GetTotalObjectsCount());
                    m_statsReporter.SetActiveObjects(m_innerScene.GetActiveObjectsCount());
                    frameMS = System.Environment.TickCount - frameMS;
                    m_statsReporter.addFrameMS(frameMS);
                    m_statsReporter.addPhysicsMS(physicsMS);
                    m_statsReporter.addOtherMS(otherMS);
                    m_statsReporter.SetActiveScripts(m_innerScene.GetActiveScriptsCount());
                    m_statsReporter.addScriptLines(m_innerScene.GetScriptLPS());
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
                updateLock.ReleaseMutex();
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
        }

        private void SendSimStatsPackets(SimStatsPacket pack)
        {
            List<ScenePresence> StatSendAgents = GetScenePresences();
            foreach (ScenePresence agent in StatSendAgents)
            {
                if (!agent.IsChildAgent)
                {
                    agent.ControllingClient.SendSimStats(pack);
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
            EventManager.TriggerOnBackup(m_storageManager.DataStore);
            m_backingup = false;
            //return true;
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
                Image image = OpenJPEG.DecodeToImage(asset.Data);
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
                int tc = System.Environment.TickCount;
                m_log.Info("[MAPTILE]: Generating Maptile Step 1: Terrain");
                Bitmap mapbmp = new Bitmap(256, 256);
                double[,] hm = Heightmap.GetDoubles();
                bool ShadowDebugContinue = true;
                //Color prim = Color.FromArgb(120, 120, 120);
                //LLVector3 RayEnd = new LLVector3(0, 0, 0);
                //LLVector3 RayStart = new LLVector3(0, 0, 0);
                //LLVector3 direction = new LLVector3(0, 0, -1);
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
                        //RayEnd = new LLVector3(x, y, 0);
                        //RayStart = new LLVector3(x, y, 255);

                        //direction = LLVector3.Norm(RayEnd - RayStart);
                        //AXOrigin = new Vector3(RayStart.X, RayStart.Y, RayStart.Z);
                        //AXdirection = new Vector3(direction.X, direction.Y, direction.Z);

                        //testRay = new Ray(AXOrigin, AXdirection);
                        //rt = m_innerScene.GetClosestIntersectingPrim(testRay);

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
                                    m_log.WarnFormat("[MAPIMAGE]: Your terrain is corrupted in region {0}, it might take a few minutes to generate the map image depending on the corruption level",RegionInfo.RegionName);
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
                catch (Exception)
                {
                    m_log.Warn("Failed to load StartupConfig");
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

                                            LLColor texcolor = part.Shape.Textures.DefaultTexture.RGBA;

                                            // Not sure why some of these are null, oh well.

                                            int colorr = 255 - (int)(texcolor.R * 255f);
                                            int colorg = 255 - (int)(texcolor.G * 255f);
                                            int colorb = 255 - (int)(texcolor.B * 255f);

                                            if (!(colorr == 255 && colorg == 255 && colorb == 255))
                                            {
                                                //Try to set the map spot color
                                                try
                                                {
                                                    // If the color gets goofy somehow, skip it *shakes fist at LLColor
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

                                        LLVector3 pos = part.GetWorldPosition();

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
                                            Vector3 scale = new Vector3(part.Shape.Scale.X, part.Shape.Scale.Y, part.Shape.Scale.Z);
                                            LLQuaternion llrot = part.GetWorldRotation();
                                            Quaternion rot = new Quaternion(llrot.W, llrot.X, llrot.Y, llrot.Z);
                                            scale = rot * scale;

                                            // negative scales don't work in this situation
                                            scale.x = Math.Abs(scale.x);
                                            scale.y = Math.Abs(scale.y);
                                            scale.z = Math.Abs(scale.z);

                                            // This scaling isn't very accurate and doesn't take into account the face rotation :P
                                            int mapdrawstartX = (int)(pos.X - scale.x);
                                            int mapdrawstartY = (int)(pos.Y - scale.y);
                                            int mapdrawendX = (int)(pos.X + scale.x);
                                            int mapdrawendY = (int)(pos.Y + scale.y);

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

                LLUUID lastMapRegionUUID = m_regInfo.lastMapUUID;

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

                LLUUID TerrainImageLLUUID = LLUUID.Random();

                if (lastMapRegionUUID == LLUUID.Zero || (lastMapRefresh + RefreshSeconds) < Util.UnixTimeSinceEpoch())
                {
                    m_regInfo.SaveLastMapUUID(TerrainImageLLUUID);

                    m_log.Warn("[MAPTILE]: STORING MAPTILE IMAGE");
                    //Extra protection..  probably not needed.
                }
                else
                {
                    TerrainImageLLUUID = lastMapRegionUUID;
                    m_log.Warn("[MAPTILE]: REUSING OLD MAPTILE IMAGE ID");
                }

                m_regInfo.RegionSettings.TerrainImageID = TerrainImageLLUUID;

                AssetBase asset = new AssetBase();
                asset.FullID = m_regInfo.RegionSettings.TerrainImageID;
                asset.Data = data;
                asset.Name = "terrainImage_" + m_regInfo.RegionID.ToString() + "_" + lastMapRefresh.ToString();
                asset.Description = RegionInfo.RegionName;

                asset.Type = 0;
                asset.Temporary = temporary;
                AssetCache.AddAsset(asset);
            }
            else
            {
                byte[] data = terrain.WriteJpeg2000Image("defaultstripe.png");
                if (data != null)
                {
                    LLUUID lastMapRegionUUID = m_regInfo.lastMapUUID;

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

                    LLUUID TerrainImageLLUUID = LLUUID.Random();

                    if (lastMapRegionUUID == LLUUID.Zero || (lastMapRefresh + RefreshSeconds) < Util.UnixTimeSinceEpoch())
                    {
                        m_regInfo.SaveLastMapUUID(TerrainImageLLUUID);

                        //m_log.Warn(terrainImageID);
                        //Extra protection..  probably not needed.
                    }
                    else
                    {
                        TerrainImageLLUUID = lastMapRegionUUID;
                    }

                    m_regInfo.RegionSettings.TerrainImageID = TerrainImageLLUUID;

                    AssetBase asset = new AssetBase();
                    asset.FullID = m_regInfo.RegionSettings.TerrainImageID;
                    asset.Data = data;
                    asset.Name = "terrainImage_" + m_regInfo.RegionID.ToString() + "_" + lastMapRefresh.ToString();
                    asset.Description = RegionInfo.RegionName;
                    asset.Type = 0;
                    asset.Temporary = temporary;
                    AssetCache.AddAsset(asset);
                }
            }
        }

        #endregion

        #region Load Land

        public void loadAllLandObjectsFromStorage(LLUUID regionID)
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
        public virtual void LoadPrimsFromStorage(LLUUID regionID)
        {
            m_log.Info("[SCENE]: Loading objects from datastore");

            List<SceneObjectGroup> PrimsFromDB = m_storageManager.DataStore.LoadObjects(regionID);
            foreach (SceneObjectGroup group in PrimsFromDB)
            {
                AddRestoredSceneObject(group, true, true);
                SceneObjectPart rootPart = group.GetChildPart(group.UUID);
                rootPart.ObjectFlags &= ~(uint)LLObject.ObjectFlags.Scripted;
                rootPart.TrimPermissions();
                group.CheckSculptAndLoad();
                //rootPart.DoPhysicsPropertyUpdate(UsePhysics, true);
            }

            m_log.Info("[SCENE]: Loaded " + PrimsFromDB.Count.ToString() + " SceneObject(s)");
        }

        /// <summary>
        /// Returns a new unallocated local primitive ID
        /// </summary>
        /// <returns>A brand new local primitive ID</returns>
        public uint PrimIDAllocate()
        {
            uint myID;

            _primAllocateMutex.WaitOne();
            myID = ++m_lastAllocatedLocalId;
            _primAllocateMutex.ReleaseMutex();

            return myID;
        }

        public LLVector3 GetNewRezLocation(LLVector3 RayStart, LLVector3 RayEnd, LLUUID RayTargetID, LLQuaternion rot, byte bypassRayCast, byte RayEndIsIntersection, bool frontFacesOnly, LLVector3 scale, bool FaceCenter)
        {
            LLVector3 pos = LLVector3.Zero;
            if (RayEndIsIntersection == (byte)1)
            {
                pos = RayEnd;
                return pos;
            }
            if (RayTargetID != LLUUID.Zero)
            {
                SceneObjectPart target = GetSceneObjectPart(RayTargetID);

                LLVector3 direction = LLVector3.Norm(RayEnd - RayStart);
                Vector3 AXOrigin = new Vector3(RayStart.X, RayStart.Y, RayStart.Z);
                Vector3 AXdirection = new Vector3(direction.X, direction.Y, direction.Z);

                if (target != null)
                {
                    pos = target.AbsolutePosition;
                    //m_log.Info("[OBJECT_REZ]: TargetPos: " + pos.ToString() + ", RayStart: " + RayStart.ToString() + ", RayEnd: " + RayEnd.ToString() + ", Volume: " + Util.GetDistanceTo(RayStart,RayEnd).ToString() + ", mag1: " + Util.GetMagnitude(RayStart).ToString() + ", mag2: " + Util.GetMagnitude(RayEnd).ToString());

                    // TODO: Raytrace better here

                    //EntityIntersection ei = m_innerScene.GetClosestIntersectingPrim(new Ray(AXOrigin, AXdirection));
                    Ray NewRay = new Ray(AXOrigin, AXdirection);

                    // Ray Trace against target here
                    EntityIntersection ei = target.TestIntersectionOBB(NewRay, new Quaternion(1,0,0,0), frontFacesOnly, FaceCenter);

                    // Un-comment out the following line to Get Raytrace results printed to the console.
                   // m_log.Info("[RAYTRACERESULTS]: Hit:" + ei.HitTF.ToString() + " Point: " + ei.ipoint.ToString() + " Normal: " + ei.normal.ToString());
                    float ScaleOffset = 0.5f;

                    // If we hit something
                    if (ei.HitTF)
                    {
                        LLVector3 scaleComponent = new LLVector3(ei.AAfaceNormal.x, ei.AAfaceNormal.y, ei.AAfaceNormal.z);
                        if (scaleComponent.X != 0) ScaleOffset = scale.X;
                        if (scaleComponent.Y != 0) ScaleOffset = scale.Y;
                        if (scaleComponent.Z != 0) ScaleOffset = scale.Z;
                        ScaleOffset = Math.Abs(ScaleOffset);
                        LLVector3 intersectionpoint = new LLVector3(ei.ipoint.x, ei.ipoint.y, ei.ipoint.z);
                        LLVector3 normal = new LLVector3(ei.normal.x, ei.normal.y, ei.normal.z);
                        // Set the position to the intersection point
                        LLVector3 offset = (normal * (ScaleOffset / 2f));
                        pos = (intersectionpoint + offset);

                        // Un-offset the prim (it gets offset later by the consumer method)
                        pos.Z -= 0.25F;
                    }

                    return pos;
                }
                else
                {
                    // We don't have a target here, so we're going to raytrace all the objects in the scene.

                    EntityIntersection ei = m_innerScene.GetClosestIntersectingPrim(new Ray(AXOrigin, AXdirection), true, false);

                    // Un-comment the following line to print the raytrace results to the console.
                    //m_log.Info("[RAYTRACERESULTS]: Hit:" + ei.HitTF.ToString() + " Point: " + ei.ipoint.ToString() + " Normal: " + ei.normal.ToString());

                    if (ei.HitTF)
                    {
                        pos = new LLVector3(ei.ipoint.x, ei.ipoint.y, ei.ipoint.z);
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

        public virtual void AddNewPrim(LLUUID ownerID, LLVector3 RayEnd, LLQuaternion rot, PrimitiveBaseShape shape,
                                       byte bypassRaycast, LLVector3 RayStart, LLUUID RayTargetID,
                                       byte RayEndIsIntersection)
        {
            LLVector3 pos = GetNewRezLocation(RayStart, RayEnd, RayTargetID, rot, bypassRaycast, RayEndIsIntersection, true, new LLVector3(0.5f, 0.5f, 0.5f), false);

            if (ExternalChecks.ExternalChecksCanRezObject(1, ownerID, pos))
            {
                // rez ON the ground, not IN the ground
                pos.Z += 0.25F;

                AddNewPrim(ownerID, pos, rot, shape);
            }
        }

        public virtual SceneObjectGroup AddNewPrim(LLUUID ownerID, LLVector3 pos, LLQuaternion rot, PrimitiveBaseShape shape)
        {
            //m_log.DebugFormat(
            //    "[SCENE]: Scene.AddNewPrim() called for agent {0} in {1}", ownerID, RegionInfo.RegionName);

            SceneObjectGroup sceneOb =
                new SceneObjectGroup(this, m_regionHandle, ownerID, PrimIDAllocate(), pos, rot, shape);

            SceneObjectPart rootPart = sceneOb.GetChildPart(sceneOb.UUID);
            // if grass or tree, make phantom
            //rootPart.TrimPermissions();
            if ((rootPart.Shape.PCode == (byte)PCode.Grass) || (rootPart.Shape.PCode == (byte)PCode.Tree) || (rootPart.Shape.PCode == (byte)PCode.NewTree))
            {
                rootPart.AddFlag(LLObject.ObjectFlags.Phantom);
                //rootPart.ObjectFlags += (uint)LLObject.ObjectFlags.Phantom;
                if (rootPart.Shape.PCode != (byte)PCode.Grass)
                    AdaptTree(ref shape);
            }

            AddNewSceneObject(sceneOb, true);

            return sceneOb;
        }

        void AdaptTree(ref PrimitiveBaseShape tree)
        {
            // Tree size has to be adapted depending on its type
            switch ((Tree)tree.State)
            {
                case Tree.Cypress1:
                case Tree.Cypress2:
                    tree.Scale = new LLVector3(4, 4, 10);
                    break;

                // case... other tree types
                // tree.Scale = new LLVector3(?, ?, ?);
                // break;

                default:
                    tree.Scale = new LLVector3(4, 4, 4);
                    break;
            }
        }

        public SceneObjectGroup AddTree(LLUUID uuid, LLVector3 scale, LLQuaternion rotation, LLVector3 position,
                                        Tree treeType, bool newTree)
        {
            PrimitiveBaseShape treeShape = new PrimitiveBaseShape();
            treeShape.PathCurve = 16;
            treeShape.PathEnd = 49900;
            treeShape.PCode = newTree ? (byte)PCode.NewTree : (byte)PCode.Tree;
            treeShape.Scale = scale;
            treeShape.State = (byte)treeType;
            return AddNewPrim(uuid, position, rotation, treeShape);
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
            return m_innerScene.AddRestoredSceneObject(sceneObject, attachToBackup, alreadyPersisted);
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
            return m_innerScene.AddNewSceneObject(sceneObject, attachToBackup);
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
                        DeleteSceneObject((SceneObjectGroup)e);
                }
            }        
        }
        
        /// <summary>
        /// Delete the given object from the scene.
        /// </summary>
        /// <param name="group"></param>
        public void DeleteSceneObject(SceneObjectGroup group)
        {
            SceneObjectPart rootPart = group.GetChildPart(group.UUID);

            if (rootPart.PhysActor != null)
            {
                PhysicsScene.RemovePrim(rootPart.PhysActor);
                rootPart.PhysActor = null;
            }

            if (UnlinkSceneObject(group.UUID, false))
            {
                EventManager.TriggerObjectBeingRemovedFromScene(group);
                EventManager.TriggerParcelPrimCountTainted();
            }

            group.DeleteGroup();
        }

        /// <summary>
        /// Unlink the given object from the scene.  Unlike delete, this just removes the record of the object - the
        /// object itself is not destroyed.
        /// </summary>
        /// <param name="uuid"></param>
        /// <returns>true if the object was in the scene, false if it was not</returns>
        public bool UnlinkSceneObject(LLUUID uuid, bool resultOfLinkingObjects)
        {
            if (m_innerScene.DeleteSceneObject(uuid,resultOfLinkingObjects))
            {
                m_storageManager.DataStore.RemoveObject(uuid, m_regInfo.RegionID);

                return true;
            }

            return false;
        }

        public void LoadPrimsFromXml(string fileName, bool newIdsFlag, LLVector3 loadOffset)
        {
            m_log.InfoFormat("[SCENE]: Loading prims in xml format to region {0} from {1}", RegionInfo.RegionName);            
            
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

        public void SavePrimsToXml2(TextWriter stream, LLVector3 min, LLVector3 max)
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
        /// Locate New region Handle and offset the prim position for the new region
        ///
        /// </summary>
        /// <param name="position">current position of Group</param>
        /// <param name="grp">Scene Object Group that we're crossing</param>

        public void CrossPrimGroupIntoNewRegion(LLVector3 position, SceneObjectGroup grp)
        {
            if (grp == null)
                return;
            if (grp.RootPart == null)
                return;

            if (grp.RootPart.DIE_AT_EDGE)
            {
                // We remove the object here
                try
                {
                    DeleteSceneObject(grp);
                }
                catch (Exception)
                {
                    m_log.Warn("[DATABASE]: exception when trying to remove the prim that crossed the border.");
                }
                return;
            }

            m_log.Warn("Prim crossing: " + grp.UUID.ToString());
            int thisx = (int)RegionInfo.RegionLocX;
            int thisy = (int)RegionInfo.RegionLocY;

            ulong newRegionHandle = 0;
            LLVector3 pos = position;

            if (position.X > Constants.RegionSize + 0.1f)
            {
                pos.X = ((pos.X - Constants.RegionSize));

                newRegionHandle = Util.UIntsToLong((uint)((thisx + 1) * Constants.RegionSize), (uint)(thisy * Constants.RegionSize));

                // x + 1
            }
            else if (position.X < -0.1f)
            {
                pos.X = ((pos.X + Constants.RegionSize));
                newRegionHandle = Util.UIntsToLong((uint)((thisx - 1) * Constants.RegionSize), (uint)(thisy * Constants.RegionSize));
                // x - 1
            }

            if (position.Y > Constants.RegionSize + 0.1f)
            {
                pos.Y = ((pos.Y - Constants.RegionSize));
                newRegionHandle = Util.UIntsToLong((uint)(thisx * Constants.RegionSize), (uint)((thisy + 1) * Constants.RegionSize));
                // y + 1
            }
            else if (position.Y < -1f)
            {
                pos.Y = ((pos.Y + Constants.RegionSize));
                newRegionHandle = Util.UIntsToLong((uint)(thisx * Constants.RegionSize), (uint)((thisy - 1) * Constants.RegionSize));
                // y - 1
            }

            // Offset the positions for the new region across the border
            grp.OffsetForNewRegion(pos);

            CrossPrimGroupIntoNewRegion(newRegionHandle, grp);
        }

        public void CrossPrimGroupIntoNewRegion(ulong newRegionHandle, SceneObjectGroup grp)
        {
            int primcrossingXMLmethod = 0;
            if (newRegionHandle != 0)
            {
                bool successYN = false;

                successYN
                    = m_sceneGridService.PrimCrossToNeighboringRegion(
                        newRegionHandle, grp.UUID, m_serialiser.SaveGroupToXml2(grp), primcrossingXMLmethod);

                if (successYN)
                {
                    // We remove the object here
                    try
                    {
                        DeleteSceneObject(grp);
                    }
                    catch (Exception)
                    {
                        m_log.Warn("[DATABASE]: exception when trying to remove the prim that crossed the border.");
                    }
                }
                else
                {
                    m_log.Warn("[INTERREGION]: Prim Crossing Failed!");
                    if (grp.RootPart != null)
                    {
                        if (grp.RootPart.PhysActor != null)
                        {
                            grp.RootPart.PhysActor.CrossingFailure();
                        }
                    }
                }
            }
        }

        public bool IncomingInterRegionPrimGroup(ulong regionHandle, LLUUID primID, string objXMLData, int XMLMethod)
        {
            m_log.Warn("{[INTERREGION]: A new prim arrived from a neighbor");
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
                            DeleteSceneObject(grp);
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
                                // Attachment
                                ScenePresence sp = GetScenePresence(grp.OwnerID);
                                if (sp != null)
                                {
                                    // hack assetID until we get assetID into the XML format.
                                    // LastOwnerID is used for group deeding, so when you do stuff
                                    // with the deeded object, it goes back to them

                                    grp.SetFromAssetID(grp.RootPart.LastOwnerID);
                                    m_innerScene.AttachObject(sp.ControllingClient, grp.LocalId, (uint)0, grp.GroupRotation, grp.AbsolutePosition);
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

        /// <summary>
        /// Register the new client with the scene
        /// </summary>
        /// <param name="client"></param
        /// <param name="child"></param>
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

                m_innerScene.AddScenePresence(presence);

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
            EventManager.TriggerOnNewClient(client);
        }

        protected virtual void SubscribeToClientEvents(IClientAPI client)
        {
            client.OnRegionHandShakeReply += SendLayerData;
            //remoteClient.OnRequestWearables += new GenericCall(this.GetInitialPrims);
            // client.OnRequestWearables += InformClientOfNeighbours;
            client.OnAddPrim += AddNewPrim;
            client.OnUpdatePrimGroupPosition += m_innerScene.UpdatePrimPosition;
            client.OnUpdatePrimSinglePosition += m_innerScene.UpdatePrimSinglePosition;
            client.OnUpdatePrimGroupRotation += m_innerScene.UpdatePrimRotation;
            client.OnUpdatePrimGroupMouseRotation += m_innerScene.UpdatePrimRotation;
            client.OnUpdatePrimSingleRotation += m_innerScene.UpdatePrimSingleRotation;
            client.OnUpdatePrimScale += m_innerScene.UpdatePrimScale;
            client.OnUpdatePrimGroupScale += m_innerScene.UpdatePrimGroupScale;
            client.OnUpdateExtraParams += m_innerScene.UpdateExtraParam;
            client.OnUpdatePrimShape += m_innerScene.UpdatePrimShape;
            //client.OnRequestMapBlocks += RequestMapBlocks; // handled in a module now.
            client.OnUpdatePrimTexture += m_innerScene.UpdatePrimTexture;
            client.OnTeleportLocationRequest += RequestTeleportLocation;
            client.OnTeleportLandmarkRequest += RequestTeleportLandmark;
            client.OnObjectSelect += SelectPrim;
            client.OnObjectDeselect += DeselectPrim;
            client.OnGrabUpdate += m_innerScene.MoveObject;
            client.OnDeRezObject += DeRezObject;
            client.OnRezObject += RezObject;
            client.OnRezSingleAttachmentFromInv += m_innerScene.RezSingleAttachment;
            client.OnDetachAttachmentIntoInv += m_innerScene.DetachSingleAttachmentToInv;
            client.OnObjectAttach += m_innerScene.AttachObject;
            client.OnObjectDetach += m_innerScene.DetachObject;
            client.OnNameFromUUIDRequest += CommsManager.HandleUUIDNameRequest;
            client.OnObjectDescription += m_innerScene.PrimDescription;
            client.OnObjectName += m_innerScene.PrimName;
            client.OnLinkObjects += m_innerScene.LinkObjects;
            client.OnDelinkObjects += m_innerScene.DelinkObjects;
            client.OnObjectDuplicate += m_innerScene.DuplicateObject;
            client.OnObjectDuplicateOnRay += doObjectDuplicateOnRay;
            client.OnUpdatePrimFlags += m_innerScene.UpdatePrimFlags;
            client.OnRequestObjectPropertiesFamily += m_innerScene.RequestObjectPropertiesFamily;
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
            client.OnObjectIncludeInSearch += m_innerScene.MakeObjectSearchable;
            client.OnTeleportHomeRequest += TeleportClientHome;
            client.OnSetStartLocationRequest += SetHomeRezPoint;
            client.OnUndo += m_innerScene.HandleUndo;
            client.OnObjectGroupRequest += m_innerScene.HandleObjectGroupUpdate;
            client.OnParcelReturnObjectsRequest += LandChannel.ReturnObjectsInParcel;
            client.OnScriptReset += ProcessScriptReset;
            client.OnGetScriptRunning += GetScriptRunning;
            client.OnSetScriptRunning += SetScriptRunning;
            
            client.OnRegionHandleRequest += RegionHandleRequest;

            client.OnUnackedTerrain += TerrainUnAcked;

            // EventManager.TriggerOnNewClient(client);
        }

        public virtual void TeleportClientHome(LLUUID agentId, IClientAPI client)
        {
            UserProfileData UserProfile = CommsManager.UserService.GetUserProfile(agentId);
            if (UserProfile != null)
            {
                LLUUID homeRegionID = UserProfile.HomeRegionID;
                ulong homeRegionHandle = UserProfile.HomeRegion;
                if (homeRegionID == LLUUID.Zero)
                {
                    RegionInfo info = CommsManager.GridService.RequestNeighbourInfo(UserProfile.HomeRegion);
                    if (info == null)
                    {
                        // can't find the region: Tell viewer and abort
                        client.SendTeleportFailed("Your home-region could not be found.");
                        return;
                    }
                    UserProfile.HomeRegionID = info.RegionID;
                    CommsManager.UserService.UpdateUserProfileProperties(UserProfile);
                }
                else
                {
                    RegionInfo info = CommsManager.GridService.RequestNeighbourInfo(homeRegionID);
                    if (info == null)
                    {
                        // can't find the region: Tell viewer and abort
                        client.SendTeleportFailed("Your home-region could not be found.");
                        return;
                    }
                    homeRegionHandle = info.RegionHandle;
                }
                RequestTeleportLocation(client, homeRegionHandle, UserProfile.HomeLocation, UserProfile.HomeLookAt, (uint)0);
            }
        }

        public void doObjectDuplicateOnRay(uint localID, uint dupeFlags, LLUUID AgentID, LLUUID GroupID,
                                           LLUUID RayTargetObj, LLVector3 RayEnd, LLVector3 RayStart,
                                           bool BypassRaycast, bool RayEndIsIntersection, bool CopyCenters, bool CopyRotates)
        {
            LLVector3 pos;
            const bool frontFacesOnly = true;
            //m_log.Info("HITTARGET: " + RayTargetObj.ToString() + ", COPYTARGET: " + localID.ToString());
            SceneObjectPart target = GetSceneObjectPart(localID);
            SceneObjectPart target2 = GetSceneObjectPart(RayTargetObj);

            if (target != null && target2 != null)
            {
                LLVector3 direction = LLVector3.Norm(RayEnd - RayStart);
                Vector3 AXOrigin = new Vector3(RayStart.X, RayStart.Y, RayStart.Z);
                Vector3 AXdirection = new Vector3(direction.X, direction.Y, direction.Z);

                if (target2.ParentGroup != null)
                {
                    pos = target2.AbsolutePosition;
                    //m_log.Info("[OBJECT_REZ]: TargetPos: " + pos.ToString() + ", RayStart: " + RayStart.ToString() + ", RayEnd: " + RayEnd.ToString() + ", Volume: " + Util.GetDistanceTo(RayStart,RayEnd).ToString() + ", mag1: " + Util.GetMagnitude(RayStart).ToString() + ", mag2: " + Util.GetMagnitude(RayEnd).ToString());

                    // TODO: Raytrace better here

                    //EntityIntersection ei = m_innerScene.GetClosestIntersectingPrim(new Ray(AXOrigin, AXdirection));
                    Ray NewRay = new Ray(AXOrigin, AXdirection);

                    // Ray Trace against target here
                    EntityIntersection ei = target2.TestIntersectionOBB(NewRay, new Quaternion(1, 0, 0, 0), frontFacesOnly, CopyCenters);

                    // Un-comment out the following line to Get Raytrace results printed to the console.
                    //m_log.Info("[RAYTRACERESULTS]: Hit:" + ei.HitTF.ToString() + " Point: " + ei.ipoint.ToString() + " Normal: " + ei.normal.ToString());
                    float ScaleOffset = 0.5f;

                    // If we hit something
                    if (ei.HitTF)
                    {
                        LLVector3 scale = target.Scale;
                        LLVector3 scaleComponent = new LLVector3(ei.AAfaceNormal.x, ei.AAfaceNormal.y, ei.AAfaceNormal.z);
                        if (scaleComponent.X != 0) ScaleOffset = scale.X;
                        if (scaleComponent.Y != 0) ScaleOffset = scale.Y;
                        if (scaleComponent.Z != 0) ScaleOffset = scale.Z;
                        ScaleOffset = Math.Abs(ScaleOffset);
                        LLVector3 intersectionpoint = new LLVector3(ei.ipoint.x, ei.ipoint.y, ei.ipoint.z);
                        LLVector3 normal = new LLVector3(ei.normal.x, ei.normal.y, ei.normal.z);
                        LLVector3 offset = normal * (ScaleOffset / 2f);
                        pos = intersectionpoint + offset;

                        // stick in offset format from the original prim
                        pos = pos - target.ParentGroup.AbsolutePosition;
                        if (CopyRotates)
                        {
                            LLQuaternion worldRot = target2.GetWorldRotation();

                            // SceneObjectGroup obj = m_innerScene.DuplicateObject(localID, pos, target.GetEffectiveObjectFlags(), AgentID, GroupID, new Quaternion(worldRot.W,worldRot.X,worldRot.Y,worldRot.Z));
                            m_innerScene.DuplicateObject(localID, pos, target.GetEffectiveObjectFlags(), AgentID, GroupID, new Quaternion(worldRot.W,worldRot.X,worldRot.Y,worldRot.Z));
                            //obj.Rotation = new Quaternion(worldRot.W, worldRot.X, worldRot.Y, worldRot.Z);
                            //obj.UpdateGroupRotation(worldRot);
                        }
                        else
                        {
                            m_innerScene.DuplicateObject(localID, pos, target.GetEffectiveObjectFlags(), AgentID, GroupID);
                        }
                    }

                    return;
                }

                return;
            }
        }

        public virtual void SetHomeRezPoint(IClientAPI remoteClient, ulong regionHandle, LLVector3 position, LLVector3 lookAt, uint flags)
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
                CommsManager.UserService.UpdateUserProfileProperties(UserProfile);

                remoteClient.SendAgentAlertMessage("Set home to here if supported by login service",false);
            }
            else
            {
                remoteClient.SendAgentAlertMessage("Set Home request Failed",false);
            }
        }

        protected virtual ScenePresence CreateAndAddScenePresence(IClientAPI client, bool child)
        {
            AvatarAppearance appearance = null;
            GetAvatarAppearance(client, out appearance);

            ScenePresence avatar = m_innerScene.CreateAndAddScenePresence(client, child, appearance);

            return avatar;
        }

        /// <summary>
        /// Get the avatar apperance for the given client.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="appearance"></param>
        public void GetAvatarAppearance(IClientAPI client, out AvatarAppearance appearance)
        {
            appearance = null;  // VS needs this line, mono doesn't
            
            try
            {
                if (m_AvatarFactory == null ||
                    !m_AvatarFactory.TryGetAvatarAppearance(client.AgentId, out appearance))
                {
                    m_log.Warn("[APPEARANCE]: Appearance not found, creating default");
                    appearance = new AvatarAppearance();
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat(
                    "[APPERANCE]: Problem when fetching appearance for avatar {0}, {1}, using default. {2}", 
                    client.Name, client.AgentId, e);
                appearance = new AvatarAppearance();
            }                
        }

        /// <summary>
        /// Remove the given client from the scene.
        /// </summary>
        /// <param name="agentID"></param>
        public override void RemoveClient(LLUUID agentID)
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
                    m_innerScene.removeUserCount(false);
                }
                else
                {
                    m_innerScene.removeUserCount(true);
                    m_sceneGridService.LogOffUser(agentID, RegionInfo.RegionID, RegionInfo.RegionHandle,
                                                  avatar.AbsolutePosition.X, avatar.AbsolutePosition.Y,
                                                  avatar.AbsolutePosition.Z);
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

            m_innerScene.RemoveScenePresence(agentID);

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

        public void HandleRemoveKnownRegionsFromAvatar(LLUUID avatarID, List<ulong> regionslst)
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
            Broadcast(delegate(IClientAPI client) { client.SendKillObject(m_regionHandle, localID); });
        }

        #endregion

        #region RegionComms

        /// <summary>
        ///
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
            m_sceneGridService.KillObject += SendKillObject;
            m_sceneGridService.OnGetLandData += GetLandData;
        }

        /// <summary>
        ///
        /// </summary>
        public void UnRegisterReginWithComms()
        {
            m_sceneGridService.KillObject -= SendKillObject;
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
        /// Do the work necessary to initiate a new user connection.
        /// At the moment, this consists of setting up the caps infrastructure
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="agent"></param>
        public void NewUserConnection(ulong regionHandle, AgentCircuitData agent)
        {
            if (regionHandle == m_regInfo.RegionHandle)
            {
                if (m_regInfo.EstateSettings.IsBanned(agent.AgentID))
                {
                    m_log.WarnFormat(
                   "[CONNECTION DEBUGGING]: Denied access to: {0} [{1}] at {2} because the user is on the region banlist",
                   agent.AgentID, regionHandle, RegionInfo.RegionName);
                }

                capsPaths[agent.AgentID] = agent.CapsPath;

                if (!agent.child)
                {
                    AddCapsHandler(agent.AgentID);

                    // Honor parcel landing type and position.
                    ILandObject land = LandChannel.GetLandObject(agent.startpos.X, agent.startpos.Y);
                    if (land != null)
                    {
                        if (land.landData.LandingType == (byte)1 && land.landData.UserLocation != LLVector3.Zero)
                        {
                            agent.startpos = land.landData.UserLocation;
                        }
                    }
                }

                m_log.DebugFormat(
                    "[CONNECTION DEBUGGING]: Creating new circuit code ({0}) for avatar {1} at {2}",
                    agent.circuitcode, agent.AgentID, RegionInfo.RegionName);

                m_authenticateHandler.AddNewCircuit(agent.circuitcode, agent);
                // rewrite session_id
                CachedUserInfo userinfo = CommsManager.UserProfileCacheService.GetUserDetails(agent.AgentID);
                userinfo.SessionID = agent.SessionID;
            }
            else
            {
                m_log.WarnFormat(
                    "[CONNECTION DEBUGGING]: Skipping this region for welcoming avatar {0} [{1}] at {2}",
                    agent.AgentID, regionHandle, RegionInfo.RegionName);
            }
        }

        protected void HandleLogOffUserFromGrid(ulong regionHandle, LLUUID AvatarID, LLUUID RegionSecret, string message)
        {
            if (RegionInfo.RegionHandle == regionHandle)
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
        public void AddCapsHandler(LLUUID agentId)
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
            cap.GetClient = m_innerScene.GetControllingClient;
            m_capsHandlers[agentId] = cap;
        }

        /// <summary>
        /// Remove the caps handler for a given agent.
        /// </summary>
        /// <param name="agentId"></param>
        public void RemoveCapsHandler(LLUUID agentId)
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
        ///
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="agentID"></param>
        /// <param name="position"></param>
        /// <param name="isFlying"></param>
        public virtual void AgentCrossing(ulong regionHandle, LLUUID agentID, LLVector3 position, bool isFlying)
        {
            if (regionHandle == m_regInfo.RegionHandle)
            {
                lock (m_scenePresences)
                {
                    if (m_scenePresences.ContainsKey(agentID))
                    {
                        try
                        {
                            m_scenePresences[agentID].MakeRootAgent(position, isFlying);
                        }
                        catch (Exception e)
                        {
                            m_log.Info("[SCENE]: Unable to do Agent Crossing.");
                            m_log.Debug("[SCENE]: " + e.ToString());
                        }
                        //m_innerScene.SwapRootChildAgent(false);
                    }
                }
            }
        }

        public virtual bool IncomingChildAgentDataUpdate(ulong regionHandle, ChildAgentDataUpdate cAgentData)
        {
            ScenePresence childAgentUpdate = GetScenePresence(new LLUUID(cAgentData.AgentID));
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
        public bool CloseConnection(ulong regionHandle, LLUUID agentID)
        {
            if (regionHandle == m_regionHandle)
            {
                ScenePresence presence = m_innerScene.GetScenePresence(agentID);
                if (presence != null)
                {
                    if (presence.IsChildAgent)
                    {
                        m_innerScene.removeUserCount(false);
                    }
                    else
                    {
                        m_innerScene.removeUserCount(true);
                    }
                    
                    // Tell a single agent to disconnect from the region.
                    presence.ControllingClient.SendShutdownConnectionNotice();

                    presence.ControllingClient.Close(true);
                }
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
        /// <param name="flags"></param>
        public void RequestTeleportLocation(IClientAPI remoteClient, string regionName, LLVector3 position,
                                            LLVector3 lookat, uint flags)
        {
            RegionInfo regionInfo = m_sceneGridService.RequestClosestRegion(regionName);
            if (regionInfo == null)
            {
                // can't find the region: Tell viewer and abort
                remoteClient.SendTeleportFailed("The region '" + regionName + "' could not be found.");
                return;
            }
            RequestTeleportLocation(remoteClient, regionInfo.RegionHandle, position, lookat, flags);
        }

        /// <summary>
        /// Tries to teleport agent to other region.
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="regionHandle"></param>
        /// <param name="position"></param>
        /// <param name="lookAt"></param>
        /// <param name="flags"></param>
        public void RequestTeleportLocation(IClientAPI remoteClient, ulong regionHandle, LLVector3 position,
                                            LLVector3 lookAt, uint flags)
        {
            lock (m_scenePresences)
            {
                if (m_scenePresences.ContainsKey(remoteClient.AgentId))
                {
                    m_sceneGridService.RequestTeleportToLocation(m_scenePresences[remoteClient.AgentId], regionHandle,
                                                                 position, lookAt, flags);
                }
            }
        }

        /// <summary>
        /// Tries to teleport agent to landmark.
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="regionHandle"></param>
        /// <param name="position"></param>
        public void RequestTeleportLandmark(IClientAPI remoteClient, LLUUID regionID, LLVector3 position)
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
                                                                 position, LLVector3.Zero, 0);
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
        public bool InformNeighbourOfCrossing(ulong regionHandle, LLUUID agentID, LLVector3 position, bool isFlying)
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
                ModuleInterfaces.Add(typeof(M), mod);
            }
        }

        /// <summary>
        /// For the given interface, retrieve the region module which implements it.
        /// </summary>
        /// <returns>null if there is no module implementing that interface</returns>
        public T RequestModuleInterface<T>()
        {
            if (ModuleInterfaces.ContainsKey(typeof(T)))
            {
                return (T)ModuleInterfaces[typeof(T)];
            }
            else
            {
                return default(T);
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

        public List<FriendListItem> GetFriendList(LLUUID avatarID)
        {
            return CommsManager.GetUserFriendList(avatarID);
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
        public void SendUrlToUser(LLUUID avatarID, string objectName, LLUUID objectID, LLUUID ownerID, bool groupOwned,
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

        public void SendDialogToUser(LLUUID avatarID, string objectName, LLUUID objectID, LLUUID ownerID, string message, LLUUID TextureID, int ch, string[] buttonlabels)
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
        public LLUUID MakeHttpRequest(string url, string type, string body)
        {
            if (m_httpRequestModule != null)
            {
                return m_httpRequestModule.MakeHttpRequest(url, type, body);
            }
            return LLUUID.Zero;
        }


        /// <summary>
        /// This method is a way for the Friends Module to create an instant
        /// message to the avatar and for Instant Messages that travel across
        /// gridcomms to make it to the Instant Message Module.
        ///
        /// Friendship establishment and groups are unfortunately tied with instant messaging and
        /// there's no way to separate them completely.
        /// </summary>
        /// <param name="message">object containing the instant message data</param>
        /// <returns>void</returns>
        public void TriggerGridInstantMessage(GridInstantMessage message, InstantMessageReceiver options)
        {
            m_eventManager.TriggerGridInstantMessage(message, options);
        }


        public virtual void StoreAddFriendship(LLUUID ownerID, LLUUID friendID, uint perms)
        {
            // TODO: m_sceneGridService.DoStuff;
            m_sceneGridService.AddNewUserFriend(ownerID, friendID, perms);
        }

        public virtual void StoreUpdateFriendship(LLUUID ownerID, LLUUID friendID, uint perms)
        {
            // TODO: m_sceneGridService.DoStuff;
            m_sceneGridService.UpdateUserFriendPerms(ownerID, friendID, perms);
        }

        public virtual void StoreRemoveFriendship(LLUUID ownerID, LLUUID ExfriendID)
        {
            // TODO: m_sceneGridService.DoStuff;
            m_sceneGridService.RemoveUserFriend(ownerID, ExfriendID);
        }
        public virtual List<FriendListItem> StoreGetFriendsForUser(LLUUID ownerID)
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

        private void SendPermissionAlert(LLUUID user, string reason)
        {
            SendAlertToUser(user, reason, false);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="message"></param>
        public void SendGeneralAlert(string message)
        {
            List<ScenePresence> presenceList = GetScenePresences();

            foreach (ScenePresence presence in presenceList)
            {
                presence.ControllingClient.SendAlertMessage(message);
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="agentID"></param>
        /// <param name="message"></param>
        /// <param name="modal"></param>
        public void SendAlertToUser(LLUUID agentID, string message, bool modal)
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
        public void handleRequestGodlikePowers(LLUUID agentID, LLUUID sessionID, LLUUID token, bool godLike,
                                               IClientAPI controllingClient)
        {
            lock (m_scenePresences)
            {
                // User needs to be logged into this sim
                if (m_scenePresences.ContainsKey(agentID))
                {
                    // First check that this is the sim owner
                    if (ExternalChecks.ExternalChecksCanBeGodLike(agentID))
                    {
                        // Next we check for spoofing.....
                        LLUUID testSessionID = m_scenePresences[agentID].ControllingClient.SessionId;
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
        public void SendRegionMessageFromEstateTools(LLUUID FromAvatarID, LLUUID fromSessionID, String FromAvatarName, String Message)
        {

            List<ScenePresence> presenceList = GetScenePresences();

            foreach (ScenePresence presence in presenceList)
            {
                if (!presence.IsChildAgent)
                    presence.ControllingClient.SendBlueBoxMessage(FromAvatarID, fromSessionID, FromAvatarName, Message);
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
        public void SendEstateMessageFromEstateTools(LLUUID FromAvatarID, LLUUID fromSessionID, String FromAvatarName, String Message)
        {

            ClientManager.ForEachClient(delegate(IClientAPI controller)
                                        {
                                            controller.SendBlueBoxMessage(FromAvatarID, fromSessionID, FromAvatarName, Message);
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
        public void HandleGodlikeKickUser(LLUUID godID, LLUUID sessionID, LLUUID agentID, uint kickflags, byte[] reason)
        {
            // For some reason the client sends this seemingly hard coded UUID for kicking everyone.   Dun-know.
            LLUUID kickUserID = new LLUUID("44e87126e7944ded05b37c42da3d5cdb");
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
                                                                controller.Kick(Helpers.FieldToUTF8String(reason));
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
                            m_innerScene.removeUserCount(!m_scenePresences[agentID].IsChildAgent);

                            m_scenePresences[agentID].ControllingClient.Kick(Helpers.FieldToUTF8String(reason));
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

        public void HandleObjectPermissionsUpdate(IClientAPI controller, LLUUID agentID, LLUUID sessionID, byte field, uint localId, uint mask, byte set)
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
                if ((presence.Firstname == firstName) && (presence.Lastname == lastName))
                {
                    presence.ControllingClient.SendAgentAlertMessage(message, modal);
                    break;
                }
            }
        }

        /// <summary>
        ///
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
                                new LLVector3(Convert.ToSingle(cmdparams[1]), Convert.ToSingle(cmdparams[2]),
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
        /// <param name="showWhat"></param>
        public void Show(string showWhat)
        {
            switch (showWhat)
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

        public LLUUID GetLandOwner(float x, float y)
        {
            ILandObject land = LandChannel.GetLandObject(x, y);
            if (land == null)
            {
                return LLUUID.Zero;
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

        public void TriggerAtTargetEvent(uint localID, uint handle, LLVector3 targetpos, LLVector3 currentpos)
        {
            m_eventManager.TriggerAtTargetEvent(localID, handle, targetpos, currentpos);
        }

        public void TriggerNotAtTargetEvent(uint localID)
        {
            m_eventManager.TriggerNotAtTargetEvent(localID);
        }

        private bool scriptDanger(SceneObjectPart part,LLVector3 pos)
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

        public bool scriptDanger(uint localID, LLVector3 pos)
        {
            SceneObjectPart part = GetSceneObjectPart(localID);
            if (part != null)
            {
                return scriptDanger(part, pos);
            }
            else
            {
                return false;
            }
        }

        public bool pipeEventsForScript(uint localID)
        {
            SceneObjectPart part = GetSceneObjectPart(localID);
            if (part != null)
            {
                // Changed so that child prims of attachments return scriptDanger for their parent, so that
                //  their scripts will actually run.
                //      -- Leaf, Tue Aug 12 14:17:05 EDT 2008
                SceneObjectPart parent = part.ParentGroup.RootPart;
                if (parent != null && parent.IsAttachment)
                    return scriptDanger(parent, parent.GetWorldPosition());
                else
                    return scriptDanger(part, part.GetWorldPosition());
            }
            else
            {
                return false;
            }
        }

        #endregion

        #region InnerScene wrapper methods

        /// <summary>
        ///
        /// </summary>
        /// <param name="localID"></param>
        /// <returns></returns>
        public LLUUID ConvertLocalIDToFullID(uint localID)
        {
            return m_innerScene.ConvertLocalIDToFullID(localID);
        }

        public void SwapRootAgentCount(bool rootChildChildRootTF)
        {
            m_innerScene.SwapRootChildAgent(rootChildChildRootTF);
        }

        public void AddPhysicalPrim(int num)
        {
            m_innerScene.AddPhysicalPrim(num);
        }

        public void RemovePhysicalPrim(int num)
        {
            m_innerScene.RemovePhysicalPrim(num);
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
            return m_innerScene.GetAvatars();
        }

        /// <summary>
        /// Return a list of all ScenePresences in this region.  This returns child agents as well as root agents.
        /// This list is a new object, so it can be iterated over without locking.
        /// </summary>
        /// <returns></returns>
        public List<ScenePresence> GetScenePresences()
        {
            return m_innerScene.GetScenePresences();
        }

        /// <summary>
        /// Request a filtered list of ScenePresences in this region.
        /// This list is a new object, so it can be iterated over without locking.
        /// </summary>
        /// <param name="filter"></param>
        /// <returns></returns>
        public List<ScenePresence> GetScenePresences(FilterAvatarList filter)
        {
            return m_innerScene.GetScenePresences(filter);
        }

        /// <summary>
        /// Request a scene presence by UUID
        /// </summary>
        /// <param name="avatarID"></param>
        /// <returns></returns>
        public ScenePresence GetScenePresence(LLUUID avatarID)
        {
            return m_innerScene.GetScenePresence(avatarID);
        }

        /// <summary>
        /// Request an Avatar's Child Status - used by ClientView when a 'kick everyone' or 'estate message' occurs
        /// </summary>
        /// <param name="avatarID">AvatarID to lookup</param>
        /// <returns></returns>
        public override bool PresenceChildStatus(LLUUID avatarID)
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
        ///
        /// </summary>
        /// <param name="localID"></param>
        /// <returns></returns>
        public SceneObjectPart GetSceneObjectPart(uint localID)
        {
            return m_innerScene.GetSceneObjectPart(localID);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="fullID"></param>
        /// <returns></returns>
        public SceneObjectPart GetSceneObjectPart(LLUUID fullID)
        {
            return m_innerScene.GetSceneObjectPart(fullID);
        }

        internal bool TryGetAvatar(LLUUID avatarId, out ScenePresence avatar)
        {
            return m_innerScene.TryGetAvatar(avatarId, out avatar);
        }

        internal bool TryGetAvatarByName(string avatarName, out ScenePresence avatar)
        {
            return m_innerScene.TryGetAvatarByName(avatarName, out avatar);
        }

        internal void ForEachClient(Action<IClientAPI> action)
        {
            m_innerScene.ForEachClient(action);
        }

        /// <summary>
        /// Returns a list of the entities in the scene.  This is a new list so operations perform on the list itself
        /// will not affect the original list of objects in the scene.
        /// </summary>
        /// <returns></returns>
        public List<EntityBase> GetEntities()
        {
            return m_innerScene.GetEntities();
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

        public void RemoveStreamHandler(string httpMethod, string path)
        {
            m_httpListener.RemoveStreamHandler(httpMethod, path);
        }

        public void RemoveHTTPHandler(string httpMethod, string path)
        {
            m_httpListener.RemoveHTTPHandler(httpMethod, path);
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
        
        public void RegionHandleRequest(IClientAPI client, LLUUID regionID)
        {
            RegionInfo info;
            if(regionID == RegionInfo.RegionID) info = RegionInfo;
            else info = CommsManager.GridService.RequestNeighbourInfo(regionID);
            if(info != null) client.SendRegionHandle(regionID, info.RegionHandle);
        }


        public void TerrainUnAcked(IClientAPI client, int patchX, int patchY)
        {
            //Console.WriteLine("Terrain packet unacked, resending patch: " + patchX + " , " + patchY);
             client.SendLayerData(patchX, patchY, Heightmap.GetFloatsSerialised());
        }
    }
}              
