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
using OpenSim.Region.Environment.Modules.Terrain;
using OpenSim.Region.Environment.Scenes.Scripting;
using OpenSim.Region.Physics.Manager;
using Caps=OpenSim.Region.Capabilities.Caps;
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

        private Random Rand = new Random();
        private uint _primCount = 720000;
        private readonly Mutex _primAllocateMutex = new Mutex(false);

        private int m_timePhase = 24;

        private readonly Mutex updateLock;
        public bool m_physicalPrim;
        public bool m_seeIntoRegionFromNeighbor;
        private int m_RestartTimerCounter;
        private readonly Timer m_restartTimer = new Timer(15000); // Wait before firing
        private int m_incrementsof15seconds = 0;

        public string m_simulatorVersion = "OpenSimulator 0.5";

        protected ModuleLoader m_moduleLoader;
        protected StorageManager m_storageManager;
        protected AgentCircuitManager m_authenticateHandler;
        public CommunicationsManager CommsManager;
        // protected XferManager xferManager;
        protected SceneCommunicationService m_sceneGridService;
        protected SceneXmlLoader m_sceneXmlLoader;

        /// <summary>
        /// Each agent has its own capabilities handler.
        /// </summary>
        protected Dictionary<LLUUID, Caps> m_capsHandlers = new Dictionary<LLUUID, Caps>();
        
        protected BaseHttpServer m_httpListener;

        protected Dictionary<string, IRegionModule> Modules = new Dictionary<string, IRegionModule>();
        public Dictionary<Type, object> ModuleInterfaces = new Dictionary<Type, object>();
        protected Dictionary<string, object> ModuleAPIMethods = new Dictionary<string, object>();
        public Dictionary<string, ICommander> m_moduleCommanders = new Dictionary<string, ICommander>();

        //API module interfaces

        public IXfer XferManager;

        protected IHttpRequests m_httpRequestModule;
        protected ISimChat m_simChatModule;
        protected IXMLRPC m_xmlrpcModule;
        protected IWorldComm m_worldCommModule;
        protected IAvatarFactory m_AvatarFactory;

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

        protected readonly EstateManager m_estateManager;
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

        public EstateManager EstateManager
        {
            get { return m_estateManager; }
        }

        public float TimeDilation
        {
            get { return m_timedilation; }
        }

        protected readonly PermissionManager m_permissionManager;
        // This is the instance to the permissions manager.  
        // This manages permissions to clients on in world objects

        public PermissionManager PermissionsMngr
        {
            get { return m_permissionManager; }
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

        public Scene(RegionInfo regInfo, AgentCircuitManager authen, PermissionManager permissionManager,
                     CommunicationsManager commsMan, SceneCommunicationService sceneGridService,
                     AssetCache assetCach, StorageManager storeManager, BaseHttpServer httpServer,
                     ModuleLoader moduleLoader, bool dumpAssetsToFile, bool physicalPrim, bool SeeIntoRegionFromNeighbor)
        {
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

            //Bind Storage Manager functions to some land manager functions for this scene
            EventManager.OnLandObjectAdded +=
                new EventManager.LandObjectAdded(m_storageManager.DataStore.StoreLandObject);
            EventManager.OnLandObjectRemoved +=
                new EventManager.LandObjectRemoved(m_storageManager.DataStore.RemoveLandObject);

            m_estateManager = new EstateManager(this, m_regInfo);

            m_permissionManager = permissionManager;
            m_permissionManager.Initialise(this);

            m_innerScene = new InnerScene(this, m_regInfo, m_permissionManager);

            // If the Inner scene has an Unrecoverable error, restart this sim.
            // Currently the only thing that causes it to happen is two kinds of specific
            // Physics based crashes.
            //
            // Out of memory
            // Operating system has killed the plugin
            m_innerScene.UnRecoverableError += RestartNow;

            m_sceneXmlLoader = new SceneXmlLoader(this, m_innerScene, m_regInfo);

            RegisterDefaultSceneEvents();

            m_log.Info("[SCENE]: Creating new entitities instance");
            Entities = new Dictionary<LLUUID, EntityBase>();
            m_scenePresences = new Dictionary<LLUUID, ScenePresence>();
            //m_sceneObjects = new Dictionary<LLUUID, SceneObjectGroup>();
            m_restorePresences = new Dictionary<LLUUID, ScenePresence>();

            m_httpListener = httpServer;
            m_dumpAssetsToFile = dumpAssetsToFile;

            if ((RegionInfo.EstateSettings.regionFlags & Simulator.RegionFlags.SkipScripts) == Simulator.RegionFlags.SkipScripts)
            {
                m_scripts_enabled = false;
            }
            else
            {
                m_scripts_enabled = true;
            }
            if ((RegionInfo.EstateSettings.regionFlags & Simulator.RegionFlags.SkipPhysics) == Simulator.RegionFlags.SkipPhysics)
            {
                m_physics_enabled = false;
            }
            else
            {
                m_physics_enabled = true;
            }

            m_statsReporter = new SimStatsReporter(regInfo);
            m_statsReporter.OnSendStatsResult += SendSimStatsPackets;

            m_statsReporter.SetObjectCapacity(objectCapacity);

            string OSString = "";

            if (System.Environment.OSVersion.Platform != PlatformID.Unix)
            {
                OSString = System.Environment.OSVersion.ToString();
            }
            else
            {
                OSString = Util.ReadEtcIssue();
            }
            if (OSString.Length > 45)
            {
                OSString = OSString.Substring(0, 45);
            }

            m_simulatorVersion = "OpenSimulator v0.5-SVN on " + OSString + " ChilTasks:" + m_seeIntoRegionFromNeighbor.ToString() + " PhysPrim:" + m_physicalPrim.ToString();
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

                if (!(m_neighbours.Contains(otherRegion)))
                {
                    lock (m_neighbours)
                    {
                        m_neighbours.Add(otherRegion);
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
                                ((SceneObjectGroup)ent).StopScripts();
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
                                ((SceneObjectGroup)ent).StartScripts();
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

        // This is the method that shuts down the scene.
        public override void Close()
        {
            m_log.Warn("[SCENE]: Closing down the single simulator: " + RegionInfo.RegionName);
            // Kick all ROOT agents with the message, 'The simulator is going down'
            ForEachScenePresence(delegate(ScenePresence avatar)
                                 {
                                     if (avatar.KnownChildRegions.Contains(RegionInfo.RegionHandle))
                                         avatar.KnownChildRegions.Remove(RegionInfo.RegionHandle);

                                     if (!avatar.IsChildAgent)
                                         avatar.ControllingClient.Kick("The simulator is going down.");

                                     avatar.ControllingClient.OutPacket(PacketPool.Instance.GetPacket(PacketType.DisableSimulator),
                                                                        ThrottleOutPacketType.Task);
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

        public void SetModuleInterfaces()
        {
            m_simChatModule = RequestModuleInterface<ISimChat>();
            m_httpRequestModule = RequestModuleInterface<IHttpRequests>();
            m_xmlrpcModule = RequestModuleInterface<IXMLRPC>();
            m_worldCommModule = RequestModuleInterface<IWorldComm>();
            XferManager = RequestModuleInterface<IXfer>();
            m_AvatarFactory = RequestModuleInterface<IAvatarFactory>();
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
                        UpdateStorageBackup();
    
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
                    m_statsReporter.SetObjects(m_innerScene.GetTotalObjects());
                    m_statsReporter.SetActiveObjects(m_innerScene.GetActiveObjects());
                    frameMS = System.Environment.TickCount - frameMS;
                    m_statsReporter.addFrameMS(frameMS);
                    m_statsReporter.addPhysicsMS(physicsMS);
                    m_statsReporter.addOtherMS(otherMS);
                    m_statsReporter.SetActiveScripts(m_innerScene.GetActiveScripts());
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

        //Updates the time in the viewer.
// TODO: unused
//         private void UpdateInWorldTime()
//         {
//             m_timeUpdateCount++;
//             if (m_timeUpdateCount > 600)
//             {
//                 List<ScenePresence> avatars = GetAvatars();
//                 foreach (ScenePresence avatar in avatars)
//                 {
//                     avatar.ControllingClient.SendViewerTime(m_timePhase);
//                 }

//                 m_timeUpdateCount = 0;
//                 m_timePhase++;
//                 if (m_timePhase > 94)
//                 {
//                     m_timePhase = 0;
//                 }
//             }
//         }

        private void SendSimStatsPackets(SimStatsPacket pack)
        {
            List<ScenePresence> StatSendAgents = GetScenePresences();
            foreach (ScenePresence agent in StatSendAgents)
            {
                if (!agent.IsChildAgent)
                {
                    pack.Header.Reliable = false;
                    agent.ControllingClient.OutPacket(pack, ThrottleOutPacketType.Task);
                }
            }
        }

        private void UpdateLand()
        {
            if (LandChannel != null)
            {
                if (LandChannel.isLandPrimCountTainted())
                {
                    LandChannel.performParcelPrimCountUpdate();
                }
            }
        }

        private void UpdateTerrain()
        {
            EventManager.TriggerTerrainTick();
        }

        private void UpdateStorageBackup()
        {
            Backup();
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
        /// 
        /// </summary>
        /// <returns></returns>
        public bool Backup()
        {
            EventManager.TriggerOnBackup(m_storageManager.DataStore);
            return true;
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
                    LandChannel.allowedForcefulBans = false;
                }
                else
                {
                    m_log.Info("[GRID]: Grid is allowing forceful parcel banlists");
                    LandChannel.allowedForcefulBans = true;
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
                Bitmap mapbmp = new Bitmap(256, 256);
                double[,] hm = Heightmap.GetDoubles();

                float heightvalue = 0;


                Color prim = Color.FromArgb(120, 120, 120);
                LLVector3 RayEnd = new LLVector3(0, 0, 0);
                LLVector3 RayStart = new LLVector3(0, 0, 0);
                LLVector3 direction = new LLVector3(0, 0, -1);
                //Vector3 AXOrigin = new Vector3();
                //Vector3 AXdirection = new Vector3();
                Ray testRay = new Ray();
                EntityIntersection rt = new EntityIntersection();

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
                        float tmpval = (float)hm[x, y];
                        heightvalue = (float)hm[x, y];

                        if ((float)heightvalue > m_regInfo.EstateSettings.waterHeight)
                        {
                            // scale height value
                            heightvalue = low + mid * (heightvalue - low) / mid;

                            if (heightvalue > 255)
                                heightvalue = 255;

                            if (heightvalue < 0)
                                heightvalue = 0;


                            Color green = Color.FromArgb((int)heightvalue, 100, (int)heightvalue);

                            // Y flip the cordinates
                            mapbmp.SetPixel(x, (256 - y) - 1, green);
                        }
                        else
                        {
                            // Y flip the cordinates
                            heightvalue = m_regInfo.EstateSettings.waterHeight - heightvalue;
                            if (heightvalue > 19)
                                heightvalue = 19;
                            if (heightvalue < 0)
                                heightvalue = 0;

                            heightvalue = 100 - (heightvalue * 100) / 19;

                            if (heightvalue > 255)
                                heightvalue = 255;

                            if (heightvalue < 0)
                                heightvalue = 0;

                            Color water = Color.FromArgb((int)heightvalue, (int)heightvalue, 255);
                            mapbmp.SetPixel(x, (256 - y) - 1, water);
                        }
                        //}


                    }
                    //tc = System.Environment.TickCount - tc;
                    //m_log.Info("[MAPTILE]: Completed One row in " + tc + " ms");
                }
                byte[] data;
                try
                {
                    data = OpenJPEG.EncodeFromImage(mapbmp, false);
                }
                catch (Exception)
                {
                    return;
                }

                m_regInfo.EstateSettings.terrainImageID = LLUUID.Random();
                AssetBase asset = new AssetBase();
                asset.FullID = m_regInfo.EstateSettings.terrainImageID;
                asset.Data = data;
                asset.Name = "terrainImage";
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
                    m_regInfo.EstateSettings.terrainImageID = LLUUID.Random();
                    AssetBase asset = new AssetBase();
                    asset.FullID = m_regInfo.EstateSettings.terrainImageID;
                    asset.Data = data;
                    asset.Name = "terrainImage";
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

            if (landData.Count == 0)
            {
                LandChannel.NoLandDataFromStorage();
            }
            else
            {
                LandChannel.IncomingLandObjectsFromStorage(landData);
            }
        }

        #endregion

        #region Primitives Methods

        /// <summary>
        /// Loads the World's objects
        /// </summary>
        public virtual void LoadPrimsFromStorage(bool m_permissions, LLUUID regionID)
        {
            m_log.Info("[SCENE]: Loading objects from datastore");

            List<SceneObjectGroup> PrimsFromDB = m_storageManager.DataStore.LoadObjects(regionID);
            foreach (SceneObjectGroup group in PrimsFromDB)
            {
                AddEntityFromStorage(group);
                SceneObjectPart rootPart = group.GetChildPart(group.UUID);
                rootPart.ObjectFlags &= ~(uint)LLObject.ObjectFlags.Scripted;
                rootPart.TrimPermissions();

                group.ApplyPhysics(m_physicalPrim);
                //rootPart.DoPhysicsPropertyUpdate(UsePhysics, true);
            }

            m_log.Info("[SCENE]: Loaded " + PrimsFromDB.Count.ToString() + " SceneObject(s)");
        }

        /// <summary>
        /// Returns a new unallocated primitive ID
        /// </summary>
        /// <returns>A brand new primitive ID</returns>
        public uint PrimIDAllocate()
        {
            uint myID;

            _primAllocateMutex.WaitOne();
            ++_primCount;
            myID = _primCount;
            _primAllocateMutex.ReleaseMutex();

            return myID;
        }

        public LLVector3 GetNewRezLocation(LLVector3 RayStart, LLVector3 RayEnd, LLUUID RayTargetID, LLQuaternion rot, byte bypassRayCast, byte RayEndIsIntersection)
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
                    EntityIntersection ei = target.TestIntersectionOBB(NewRay, new Quaternion(1,0,0,0));

                    // Un-comment out the following line to Get Raytrace results printed to the console.
                   // m_log.Info("[RAYTRACERESULTS]: Hit:" + ei.HitTF.ToString() + " Point: " + ei.ipoint.ToString() + " Normal: " + ei.normal.ToString());
                    
                    // If we hit something
                    if (ei.HitTF)
                    {
                        // Set the position to the intersection point
                        pos = (new LLVector3(ei.ipoint.x, ei.ipoint.y, ei.ipoint.z) + (new LLVector3(ei.normal.x,ei.normal.x,ei.normal.z) * (0.5f/2f)));
                        
                        // Un-offset the prim (it gets offset later by the consumer method)
                        pos.Z -= 0.25F;
                        
                    } 
                        
                    
                    return pos;
                }
                else
                {
                    // We don't have a target here, so we're going to raytrace all the objects in the scene.

                    EntityIntersection ei = m_innerScene.GetClosestIntersectingPrim(new Ray(AXOrigin, AXdirection));

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
            LLVector3 pos = GetNewRezLocation(RayStart, RayEnd, RayTargetID, rot, bypassRaycast, RayEndIsIntersection);

            if (PermissionsMngr.CanRezObject(ownerID, pos))
            {
                // rez ON the ground, not IN the ground
                pos.Z += 0.25F;


                AddNewPrim(ownerID, pos, rot, shape);
            }
        }

        public virtual SceneObjectGroup AddNewPrim(LLUUID ownerID, LLVector3 pos, LLQuaternion rot, PrimitiveBaseShape shape)
        {
            SceneObjectGroup sceneOb =
                new SceneObjectGroup(this, m_regionHandle, ownerID, PrimIDAllocate(), pos, rot, shape);
            AddEntity(sceneOb);
            SceneObjectPart rootPart = sceneOb.GetChildPart(sceneOb.UUID);
            // if grass or tree, make phantom
            //rootPart.TrimPermissions();
            if ((rootPart.Shape.PCode == (byte)PCode.Grass) || (rootPart.Shape.PCode == (byte)PCode.Tree) || (rootPart.Shape.PCode == (byte)PCode.NewTree))
            {
                rootPart.AddFlag(LLObject.ObjectFlags.Phantom);
                //rootPart.ObjectFlags += (uint)LLObject.ObjectFlags.Phantom;
            }
            // if not phantom, add to physics
            sceneOb.ApplyPhysics(m_physicalPrim);
            m_innerScene.AddToUpdateList(sceneOb);

            return sceneOb;
        }

        public SceneObjectGroup AddTree(LLVector3 scale, LLQuaternion rotation, LLVector3 position,
                                        Tree treeType, bool newTree)
        {
            LLUUID uuid = this.RegionInfo.MasterAvatarAssignedUUID;
            PrimitiveBaseShape treeShape = new PrimitiveBaseShape();
            treeShape.PathCurve = 16;
            treeShape.PathEnd = 49900;
            treeShape.PCode = newTree ? (byte)PCode.NewTree : (byte)PCode.Tree;
            treeShape.Scale = scale;
            treeShape.State = (byte)treeType;
            return AddNewPrim(uuid, position, rotation, treeShape);
        }

        public void RemovePrim(uint localID, LLUUID avatar_deleter)
        {
            m_innerScene.RemovePrim(localID, avatar_deleter);
        }

        public void AddEntityFromStorage(SceneObjectGroup sceneObject)
        {
            m_innerScene.AddEntityFromStorage(sceneObject);
        }

        public void AddEntity(SceneObjectGroup sceneObject)
        {
            m_innerScene.AddEntity(sceneObject);
        }

        public void RemoveEntity(SceneObjectGroup sceneObject)
        {
            if (Entities.ContainsKey(sceneObject.UUID))
            {
                LandChannel.removePrimFromLandPrimCounts(sceneObject);
                Entities.Remove(sceneObject.UUID);
                LandChannel.setPrimsTainted();
                m_innerScene.RemoveAPrimCount();
            }
        }

        /// <summary>
        /// Called by a prim when it has been created/cloned, so that its events can be subscribed to
        /// </summary>
        /// <param name="prim"></param>
        public void AcknowledgeNewPrim(SceneObjectGroup prim)
        {
            prim.OnPrimCountTainted += LandChannel.setPrimsTainted;
        }

        public void LoadPrimsFromXml(string fileName, bool newIdsFlag, LLVector3 loadOffset)
        {
            m_sceneXmlLoader.LoadPrimsFromXml(fileName, newIdsFlag, loadOffset);
        }

        public void SavePrimsToXml(string fileName)
        {
            m_sceneXmlLoader.SavePrimsToXml(fileName);
        }

        public void LoadPrimsFromXml2(string fileName)
        {
            m_sceneXmlLoader.LoadPrimsFromXml2(fileName);
        }

        public void SavePrimsToXml2(string fileName)
        {
            m_sceneXmlLoader.SavePrimsToXml2(fileName);
        }

        /// <summary>
        /// Locate New region Handle and offset the prim position for the new region
        /// 
        /// </summary>
        /// <param name="position">current position of Group</param>
        /// <param name="grp">Scene Object Group that we're crossing</param>
 
        public void CrossPrimGroupIntoNewRegion(LLVector3 position, SceneObjectGroup grp)
        {
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
                successYN = m_sceneGridService.PrimCrossToNeighboringRegion(newRegionHandle, grp.UUID, m_sceneXmlLoader.SavePrimGroupToXML2String(grp), primcrossingXMLmethod);
                if (successYN)
                {
                    // We remove the object here
                    try
                    {
                        DeleteSceneObjectGroup(grp);
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
                m_sceneXmlLoader.LoadGroupFromXml2String(objXMLData);
                SceneObjectPart RootPrim = GetSceneObjectPart(primID);
                if (RootPrim != null)
                {
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
        /// 
        /// </summary>
        /// <param name="client"></param          
        /// <param name="child"></param>
        public override void AddNewClient(IClientAPI client, bool child)
        {
            m_log.DebugFormat(
                "[CONNECTION DEBUGGING]: Creating new client for {0} at {1}", 
                client.AgentId, RegionInfo.RegionName);
            
            SubscribeToClientEvents(client);
            ScenePresence presence = null;

            if (m_restorePresences.ContainsKey(client.AgentId))
            {
                m_log.Info("[REGION]: Restore Scene Presence");

                presence = m_restorePresences[client.AgentId];
                m_restorePresences.Remove(client.AgentId);

                presence.initializeScenePresence(client, RegionInfo, this);

                m_innerScene.AddScenePresence(presence);

                lock (m_restorePresences)
                {
                    Monitor.PulseAll(m_restorePresences);
                }
            }
            else
            {
                m_log.Info("[REGION]: Add New Scene Presence");

                m_estateManager.sendRegionHandshake(client);

                CreateAndAddScenePresence(client, child);

                LandChannel.sendParcelOverlay(client);
                CommsManager.UserProfileCacheService.AddNewUser(client.AgentId);
            }
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
            client.OnRequestMapBlocks += RequestMapBlocks;
            client.OnUpdatePrimTexture += m_innerScene.UpdatePrimTexture;
            client.OnTeleportLocationRequest += RequestTeleportLocation;
            client.OnTeleportLandmarkRequest += RequestTeleportLandmark;
            client.OnObjectSelect += SelectPrim;
            client.OnObjectDeselect += DeselectPrim;
            client.OnGrabUpdate += m_innerScene.MoveObject;
            client.OnDeRezObject += DeRezObject;
            client.OnRezObject += RezObject;
            client.OnRezSingleAttachmentFromInv += m_innerScene.RezSingleAttachment;
            client.OnObjectAttach += m_innerScene.AttachObject;
            client.OnObjectDetach += m_innerScene.DetachObject;
            client.OnNameFromUUIDRequest += CommsManager.HandleUUIDNameRequest;
            client.OnObjectDescription += m_innerScene.PrimDescription;
            client.OnObjectName += m_innerScene.PrimName;
            client.OnLinkObjects += m_innerScene.LinkObjects;
            client.OnDelinkObjects += m_innerScene.DelinkObjects;
            client.OnObjectDuplicate += m_innerScene.DuplicateObject;
            client.OnUpdatePrimFlags += m_innerScene.UpdatePrimFlags;
            client.OnRequestObjectPropertiesFamily += m_innerScene.RequestObjectPropertiesFamily;
            client.OnParcelPropertiesRequest += new ParcelPropertiesRequest(LandChannel.handleParcelPropertiesRequest);
            client.OnParcelDivideRequest += new ParcelDivideRequest(LandChannel.handleParcelDivideRequest);
            client.OnParcelJoinRequest += new ParcelJoinRequest(LandChannel.handleParcelJoinRequest);
            client.OnParcelPropertiesUpdateRequest +=
                new ParcelPropertiesUpdateRequest(LandChannel.handleParcelPropertiesUpdateRequest);
            client.OnParcelSelectObjects += new ParcelSelectObjects(LandChannel.handleParcelSelectObjectsRequest);
            client.OnParcelObjectOwnerRequest +=
                new ParcelObjectOwnerRequest(LandChannel.handleParcelObjectOwnersRequest);
            client.OnParcelAccessListRequest += new ParcelAccessListRequest(LandChannel.handleParcelAccessRequest);
            client.OnParcelAccessListUpdateRequest +=
                new ParcelAccessListUpdateRequest(LandChannel.handleParcelAccessUpdateRequest);

            client.OnEstateOwnerMessage += new EstateOwnerMessageRequest(m_estateManager.handleEstateOwnerMessage);
            client.OnRegionInfoRequest += m_estateManager.HandleRegionInfoRequest;
            client.OnEstateCovenantRequest += m_estateManager.HandleEstateCovenantRequest;
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
            // client.OnAssetUploadRequest += CommsManager.TransactionsManager.HandleUDPUploadRequest;
            //  client.OnXferReceive += CommsManager.TransactionsManager.HandleXfer;
            client.OnRezScript += RezScript;

            client.OnRequestTaskInventory += RequestTaskInventory;
            client.OnRemoveTaskItem += RemoveTaskInventory;
            client.OnUpdateTaskInventory += UpdateTaskInventory;
            client.OnMoveTaskItem += MoveTaskInventoryItem;

            client.OnGrabObject += ProcessObjectGrab;
            client.OnMoneyTransferRequest += ProcessMoneyTransferRequest;
            client.OnParcelBuy += ProcessParcelBuy;
            client.OnAvatarPickerRequest += ProcessAvatarPickerRequest;
            client.OnPacketStats += AddPacketStats;

            client.OnObjectIncludeInSearch += m_innerScene.MakeObjectSearchable;

            client.OnTeleportHomeRequest += TeleportClientHome;

            client.OnSetStartLocationRequest += SetHomeRezPoint;
             
            EventManager.TriggerOnNewClient(client);
        }
        public virtual void TeleportClientHome(LLUUID AgentId, IClientAPI client)
        {
            UserProfileData UserProfile = CommsManager.UserService.GetUserProfile(AgentId);
            if (UserProfile != null)
            {
                ulong homeRegion = UserProfile.HomeRegion;
                LLVector3 homePostion = new LLVector3(UserProfile.HomeLocationX,UserProfile.HomeLocationY,UserProfile.HomeLocationZ);
                LLVector3 homeLookat = new LLVector3(UserProfile.HomeLookAt);
                RequestTeleportLocation(client, homeRegion, homePostion,homeLookat,(uint)0);

            }


        }
        
        public virtual void SetHomeRezPoint(IClientAPI remoteClient, ulong regionHandle, LLVector3 position, LLVector3 lookAt, uint flags)
        {
            UserProfileData UserProfile = CommsManager.UserService.GetUserProfile(remoteClient.AgentId);
            if (UserProfile != null)
            {
                // I know I'm ignoring the regionHandle provided by the teleport location request.
                // reusing the TeleportLocationRequest delegate, so regionHandle isn't valid
                UserProfile.HomeRegion = RegionInfo.RegionHandle;

                // We cast these to an int so as not to cause a breaking change with old regions
                // Newer regions treat this as a float on the ExpectUser method..  so we need to wait a few
                // releases before setting these to floats. (r4257)
                UserProfile.HomeLocationX = (int)position.X;
                UserProfile.HomeLocationY = (int)position.Y;
                UserProfile.HomeLocationZ = (int)position.Z;
                UserProfile.HomeLookAtX = (int)lookAt.X;
                UserProfile.HomeLookAtY = (int)lookAt.Y;
                UserProfile.HomeLookAtZ = (int)lookAt.Z;
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
            ScenePresence avatar = null;

            AvatarAppearance appearance;
            GetAvatarAppearance(client, out appearance);

            avatar = m_innerScene.CreateAndAddScenePresence(client, child, appearance);

            if (avatar.IsChildAgent)
            {
                avatar.OnSignificantClientMovement += LandChannel.handleSignificantClientMovement;
            }

            return avatar;
        }


        protected void GetAvatarAppearance(IClientAPI client, out AvatarAppearance appearance)
        {
            if (m_AvatarFactory == null ||
                !m_AvatarFactory.TryGetAvatarAppearance(client.AgentId, out appearance))
            {
                //not found Appearance
                m_log.Warn("[AVATAR DEBUGGING]: Couldn't fetch avatar appearance from factory, please report this to the opensim mantis");
                byte[] visualParams;
                AvatarWearable[] wearables;
                GetDefaultAvatarAppearance(out wearables, out visualParams);
                appearance = new AvatarAppearance(client.AgentId, wearables, visualParams);
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

            lock (m_scenePresences)
            {
                if (m_scenePresences.Remove(agentID))
                {
                    //m_log.InfoFormat("[SCENE] Removed scene presence {0}", agentID);
                }
                else
                {
                    m_log.WarnFormat("[SCENE] Tried to remove non-existent scene presence with agent ID {0} from scene ScenePresences list", agentID);
                }
            }

            lock (Entities)
            {
                if (Entities.Remove(agentID))
                {
                    //m_log.InfoFormat("[SCENE] Removed scene presence {0} from entities list", agentID);
                }
                else
                {
                    m_log.WarnFormat("[SCENE] Tried to remove non-existent scene presence with agent ID {0} from scene Entities list", agentID);
                }
            }

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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entID"></param>
        /// <returns></returns>
        public bool DeleteEntity(LLUUID entID)
        {
            if (Entities.ContainsKey(entID))
            {
                Entities.Remove(entID);
                m_storageManager.DataStore.RemoveObject(entID, m_regInfo.RegionID);
                m_innerScene.RemoveAPrimCount();
                return true;
            }
            return false;
        }

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

            m_sceneGridService.KillObject = SendKillObject;
        }

        /// <summary>
        /// 
        /// </summary>
        public void UnRegisterReginWithComms()
        {
            m_sceneGridService.OnRemoveKnownRegionFromAvatar -= HandleRemoveKnownRegionsFromAvatar;
            m_sceneGridService.OnExpectPrim -= IncomingInterRegionPrimGroup;
            m_sceneGridService.OnChildAgentUpdate -= IncomingChildAgentDataUpdate;
            m_sceneGridService.OnRegionUp -= OtherRegionUp;
            m_sceneGridService.OnExpectUser -= NewUserConnection;
            m_sceneGridService.OnAvatarCrossingIntoRegion -= AgentCrossing;
            m_sceneGridService.OnCloseAgentConnection -= CloseConnection;

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
                capsPaths[agent.AgentID] = agent.CapsPath;
                    
                if (!agent.child)
                {
                    AddCapsHandler(agent.AgentID);

                    // Honor parcel landing type and position.
                    ILandObject land = LandChannel.getLandObject(agent.startpos.X, agent.startpos.Y);
                    if (land != null)
                    {
                        if (land.landData.landingType == (byte)1 && land.landData.userLocation != LLVector3.Zero)
                        {
                            agent.startpos = land.landData.userLocation;
                        }
                    }
                }
                
                m_log.DebugFormat(
                    "[CONNECTION DEBUGGING]: Creating new circuit code ({0}) for avatar {1} at {2}",
                    agent.circuitcode, agent.AgentID, RegionInfo.RegionName);
                
                m_authenticateHandler.AddNewCircuit(agent.circuitcode, agent);
            }
            else
            {
                m_log.WarnFormat(
                    "[CONNECTION DEBUGGING]: Skipping this region for welcoming avatar {0} [{1}] at {2}", 
                    agent.AgentID, regionHandle, RegionInfo.RegionName);
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
            String capsObjectPath = GetCapsPath(agentId);                

            m_log.DebugFormat(
                "[CAPS]: Setting up CAPS handler for root agent {0} in {1}",
                agentId, RegionInfo.RegionName);
                
            Caps cap =
                new Caps(AssetCache, m_httpListener, m_regInfo.ExternalHostName, m_httpListener.Port,
                         capsObjectPath, agentId, m_dumpAssetsToFile, RegionInfo.RegionName);
            cap.RegisterHandlers();
            
            EventManager.TriggerOnRegisterCaps(agentId, cap);

            cap.AddNewInventoryItem = AddInventoryItem;
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
                    DisableSimulatorPacket disable = (DisableSimulatorPacket)PacketPool.Instance.GetPacket(PacketType.DisableSimulator);
                    presence.ControllingClient.OutPacket(disable, ThrottleOutPacketType.Unknown);
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
            m_sceneGridService.RequestMapBlocks(remoteClient, minX, minY, maxX, maxX);
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
            if (m_scenePresences.ContainsKey(remoteClient.AgentId))
            {
                m_sceneGridService.RequestTeleportToLocation(m_scenePresences[remoteClient.AgentId], regionHandle,
                                                             position, lookAt, flags);
            }
        }

        /// <summary>
        /// Tries to teleport agent to landmark.
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="regionHandle"></param>
        /// <param name="position"></param>
        public void RequestTeleportLandmark(IClientAPI remoteClient, ulong regionHandle, LLVector3 position)
        {
            if (m_scenePresences.ContainsKey(remoteClient.AgentId))
            {
                m_sceneGridService.RequestTeleportToLocation(m_scenePresences[remoteClient.AgentId], regionHandle,
                                                             position, LLVector3.Zero, 0);
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
        /// 
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
        /// 
        /// </summary>
        /// <returns></returns>
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
            if (m_scenePresences.ContainsKey(avatarID))
            {
                m_scenePresences[avatarID].ControllingClient.SendLoadURL(objectName, objectID, ownerID, groupOwned,
                                                                         message, url);
            }
        }

        public void SendDialogToUser(LLUUID avatarID, string objectName, LLUUID objectID, LLUUID ownerID, string message, LLUUID TextureID, int ch, string[] buttonlabels)
        {
            if (m_scenePresences.ContainsKey(avatarID))
            {
                m_scenePresences[avatarID].ControllingClient.SendDialog(objectName, objectID, ownerID, message, TextureID, ch, buttonlabels);
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
            if (m_scenePresences.ContainsKey(agentID))
            {
                m_scenePresences[agentID].ControllingClient.SendAgentAlertMessage(message, modal);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="agentID"></param>
        /// <param name="sessionID"></param>
        /// <param name="token"></param>
        /// <param name="controllingClient"></param>
        public void handleRequestGodlikePowers(LLUUID agentID, LLUUID sessionID, LLUUID token, bool godLike,
                                               IClientAPI controllingClient)
        {
            // First check that this is the sim owner
            if (m_permissionManager.GenericEstatePermission(agentID))
            {
                // User needs to be logged into this sim
                if (m_scenePresences.ContainsKey(agentID))
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
            }
            else
            {
                m_scenePresences[agentID].ControllingClient.SendAgentAlertMessage("Request for god powers denied", false);
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
            if (m_scenePresences.ContainsKey(agentID) || agentID == kickUserID)
            {
                if (m_permissionManager.GenericEstatePermission(godID))
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
                                                        bool childagent = !p.Equals(null) && p.IsChildAgent;
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
                        if (m_scenePresences[agentID].IsChildAgent)
                        {
                            m_innerScene.removeUserCount(false);
                        }
                        else
                        {
                            m_innerScene.removeUserCount(true);
                        }

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
            List<EntityBase> EntitieList = GetEntities();

            foreach (EntityBase ent in EntitieList)
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

            List<EntityBase> EntitieList = GetEntities();

            foreach (EntityBase ent in EntitieList)
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
            ILandObject land = LandChannel.getLandObject(x, y);
            if (land == null)
            {
                return LLUUID.Zero;
            }
            else
            {
                return land.landData.ownerID;
            }
        }

        public LandData GetLandData(float x, float y)
        {
            return LandChannel.getLandObject(x, y).landData;
        }

        public void SetLandMusicURL(float x, float y, string url)
        {
            ILandObject land = LandChannel.getLandObject(x, y);
            if (land == null)
            {
                return;
            }
            else
            {
                land.landData.musicURL = url;
                return;
            }
        }

        public void SetLandMediaURL(float x, float y, string url)
        {
            ILandObject land = LandChannel.getLandObject(x, y);

            if (land == null)
            {
                return;
            }

            else
            {
                land.landData.mediaURL = url;
                return;
            }
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
            ILandObject parcel = LandChannel.getLandObject(pos.X, pos.Y);
            if (part != null)
            {
                if (parcel != null)
                {
                    if ((parcel.landData.landFlags & (uint)Parcel.ParcelFlags.AllowOtherScripts) != 0)
                    {
                        return true;
                    }
                    else if ((parcel.landData.landFlags & (uint)Parcel.ParcelFlags.AllowGroupScripts) != 0)
                    {
                        if (part.OwnerID == parcel.landData.ownerID || (parcel.landData.isGroupOwned && part.GroupID == parcel.landData.groupID) || PermissionsMngr.GenericEstatePermission(part.OwnerID))
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
                        if (part.OwnerID == parcel.landData.ownerID)
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
                LLVector3 pos = part.GetWorldPosition();
                return scriptDanger(part, pos);

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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="presence"></param>
        public void SendAllSceneObjectsToClient(ScenePresence presence)
        {
            m_innerScene.SendAllSceneObjectsToClient(presence);
        }

        //The idea is to have a group of method that return a list of avatars meeting some requirement
        // ie it could be all m_scenePresences within a certain range of the calling prim/avatar. 

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public List<ScenePresence> GetAvatars()
        {
            return m_innerScene.GetAvatars();
        }

        /// <summary>
        /// Request a List of all m_scenePresences in this World
        /// </summary>
        /// <returns></returns>
        public List<ScenePresence> GetScenePresences()
        {
            return m_innerScene.GetScenePresences();
        }

        /// <summary>
        /// Request a filtered list of m_scenePresences in this World
        /// </summary>
        /// <param name="filter"></param>
        /// <returns></returns>
        public List<ScenePresence> GetScenePresences(FilterAvatarList filter)
        {
            return m_innerScene.GetScenePresences(filter);
        }

        /// <summary>
        /// Request a Avatar by UUID
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
            // We don't want to try to send messages if there are no avatar.
            if (!(m_scenePresences.Equals(null)))
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
        /// Delete this object from the scene.
        /// </summary>
        /// <param name="group"></param>
        public void DeleteSceneObjectGroup(SceneObjectGroup group)
        {           
            SceneObjectPart rootPart = (group).GetChildPart(group.UUID);
            if (rootPart.PhysActor != null)
            {
                PhysicsScene.RemovePrim(rootPart.PhysActor);
                rootPart.PhysActor = null;
            }

            m_storageManager.DataStore.RemoveObject(group.UUID, m_regInfo.RegionID);
            group.DeleteGroup();

            lock (Entities)
            {
                Entities.Remove(group.UUID);
                m_innerScene.RemoveAPrimCount();
            }
            group.DeleteParts();
            
            // In case anybody else retains a reference to this group, signal deletion by changing the name
            // to null.  We can't zero out the UUID because this is taken from the root part, which has already
            // been removed.
            // FIXME: This is a really poor temporary solution, since it still leaves plenty of scope for race
            // conditions where a user deletes an entity while it is being stored.  Really, the update
            // code needs a redesign.
            group.Name = null;          
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
    }
}
