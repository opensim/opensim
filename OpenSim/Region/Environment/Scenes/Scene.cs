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
* 
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
using OpenSim.Framework.Console;
using OpenSim.Framework.Servers;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.LandManagement;
using OpenSim.Region.Environment.Modules;
using OpenSim.Region.Environment.Scenes.Scripting;
using OpenSim.Region.Physics.Manager;
using OpenSim.Region.Terrain;
using Caps=OpenSim.Region.Capabilities.Caps;
using Image=System.Drawing.Image;
using Timer=System.Timers.Timer;

namespace OpenSim.Region.Environment.Scenes
{
    public delegate bool FilterAvatarList(ScenePresence avatar);

    public partial class Scene : SceneBase
    {
        #region Fields

        protected Timer m_heartbeatTimer = new Timer();
        protected Timer m_restartWaitTimer = new Timer();

        protected SimStatsReporter m_statsReporter;

        protected List<RegionInfo> m_regionRestartNotifyList = new List<RegionInfo>();
        protected List<RegionInfo> m_neighbours = new List<RegionInfo>();

        public InnerScene m_innerScene;

        private Random Rand = new Random();
        private uint _primCount = 702000;
        private readonly Mutex _primAllocateMutex = new Mutex(false);

        private int m_timePhase = 24;
        private int m_timeUpdateCount;

        private readonly Mutex updateLock;
        public bool m_physicalPrim;
        public bool m_sendTasksToChild;
        private int m_RestartTimerCounter;
        private readonly Timer m_restartTimer = new Timer(15000); // Wait before firing
        private int m_incrementsof15seconds = 0;

        protected ModuleLoader m_moduleLoader;
        protected StorageManager m_storageManager;
        protected AgentCircuitManager m_authenticateHandler;
        public CommunicationsManager CommsManager;
        // protected XferManager xferManager;
        protected SceneCommunicationService m_sceneGridService;
        protected SceneXmlLoader m_sceneXmlLoader;

        protected Dictionary<LLUUID, Caps> m_capsHandlers = new Dictionary<LLUUID, Caps>();
        protected BaseHttpServer httpListener;

        protected Dictionary<string, IRegionModule> Modules = new Dictionary<string, IRegionModule>();
        public Dictionary<Type, object> ModuleInterfaces = new Dictionary<Type, object>();
        protected Dictionary<string, object> ModuleAPIMethods = new Dictionary<string, object>();

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
        protected float m_timespan = 0.1f;
        protected DateTime m_lastupdate = DateTime.Now;

        protected float m_timedilation = 1.0f;

        private int m_update_physics = 1;
        private int m_update_entitymovement = 1;
        private int m_update_entities = 1;
        private int m_update_events = 1;
        private int m_update_backup = 200;
        private int m_update_terrain = 50;
        private int m_update_land = 1;
        private int m_update_avatars = 1;

        private int frameMS = 0;
        private int physicsMS2 = 0;
        private int physicsMS = 0;
        private int otherMS = 0;

        private bool m_physics_enabled = true;
        private bool m_physics_collisions_enabled = true;
        private bool m_scripts_enabled = true;



        #endregion

        #region Properties

        public AgentCircuitManager AuthenticateHandler
        {
            get { return m_authenticateHandler; }
        }

        protected readonly LandManager m_LandManager;
        // LandManager object instance that manages land related things.  Parcel, primcounts etc..  
        public LandManager LandManager
        {
            get { return m_LandManager; }
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

        #endregion

        #region Constructors

        public Scene(RegionInfo regInfo, AgentCircuitManager authen, PermissionManager permissionManager,
                     CommunicationsManager commsMan, SceneCommunicationService sceneGridService,
                     AssetCache assetCach, StorageManager storeManager, BaseHttpServer httpServer,
                     ModuleLoader moduleLoader, bool dumpAssetsToFile, bool physicalPrim, bool SendTasksToChild)
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
            m_sendTasksToChild = SendTasksToChild;

            m_eventManager = new EventManager();

            m_LandManager = new LandManager(this, m_regInfo);

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

            m_log.Info("[SCENE]: Creating LandMap");
            Terrain = new TerrainEngine((int) RegionInfo.RegionLocX, (int) RegionInfo.RegionLocY);

            httpListener = httpServer;
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
        }

        #endregion

        #region Startup / Close Methods

        protected virtual void RegisterDefaultSceneEvents()
        {
            m_eventManager.OnParcelPrimCountAdd += m_LandManager.addPrimToLandPrimCounts;
            m_eventManager.OnParcelPrimCountUpdate += addPrimsToParcelCounts;
            m_eventManager.OnPermissionError += SendPermissionAlert;
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
                    ( resultY <= 1))
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
                m_incrementsof15seconds = (int) seconds/15;
                m_RestartTimerCounter = 0;
                m_restartTimer.AutoReset = true;
                m_restartTimer.Elapsed += new ElapsedEventHandler(RestartTimer_Elapsed);
                m_log.Error("[REGION]: Restarting Region in " + (seconds/60) + " minutes");
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

                                         avatar.ControllingClient.OutPacket(PacketPool.Instance.GetPacket(libsecondlife.Packets.PacketType.DisableSimulator),
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
        /// 
        /// </summary>
        public void StartTimer()
        {
            m_log.Debug("[SCENE]: Starting timer");
            m_heartbeatTimer.Enabled = true;
            m_heartbeatTimer.Interval = (int) (m_timespan*1000);
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
            
            frameMS = System.Environment.TickCount;
            try
            {
                // Increment the frame counter
                m_frame++;

                // Loop it
                if (m_frame == Int32.MaxValue)
                    m_frame = 0;
                

                            physicsMS2 = System.Environment.TickCount;
                if ((m_frame%m_update_physics == 0) && m_physics_enabled)
                    m_innerScene.UpdatePreparePhysics();
                            physicsMS2 = System.Environment.TickCount - physicsMS2;

                if (m_frame%m_update_entitymovement == 0)
                    m_innerScene.UpdateEntityMovement();

                            physicsMS = System.Environment.TickCount;
                if ((m_frame%m_update_physics == 0) && m_physics_enabled)
                    physicsFPS = m_innerScene.UpdatePhysics(
                        Math.Max(SinceLastFrame.TotalSeconds, m_timespan)
                        );
                
                            physicsMS = System.Environment.TickCount - physicsMS;
                physicsMS += physicsMS2;

                            otherMS = System.Environment.TickCount;
                if (m_frame%m_update_entities == 0)
                    m_innerScene.UpdateEntities();

                if (m_frame%m_update_events == 0)
                    UpdateEvents();

                if (m_frame%m_update_backup == 0)
                    UpdateStorageBackup();

                if (m_frame%m_update_terrain == 0)
                    UpdateTerrain();

                if (m_frame%m_update_land == 0)
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
            catch (NotImplementedException)
            {
                throw;
            }
            catch (Exception e)
            {
                m_log.Error("[Scene]: Failed with exception " + e.ToString());
            }
            finally
            {
                updateLock.ReleaseMutex();

                m_timedilation = m_timespan/(float) SinceLastFrame.TotalSeconds;
                m_lastupdate = DateTime.Now;
            }
        }

        //Updates the time in the viewer.
        private void UpdateInWorldTime()
        {
            m_timeUpdateCount++;
            if (m_timeUpdateCount > 600)
            {
                List<ScenePresence> avatars = GetAvatars();
                foreach (ScenePresence avatar in avatars)
                {
                    avatar.ControllingClient.SendViewerTime(m_timePhase);
                }

                m_timeUpdateCount = 0;
                m_timePhase++;
                if (m_timePhase > 94)
                {
                    m_timePhase = 0;
                }
            }
        }

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
            if (m_LandManager.landPrimCountTainted)
            {
                //Perform land update of prim count
                performParcelPrimCountUpdate();
            }
        }

        private void UpdateTerrain()
        {
            if (Terrain.IsTainted() && !Terrain.IsUserStillEditing())
            {
                CreateTerrainTexture(true);

                lock (Terrain.heightmap)
                {
                    lock (SyncRoot)
                    {
                        PhysicsScene.SetTerrain(Terrain.GetHeights1D());
                    }

                    m_storageManager.DataStore.StoreTerrain(Terrain.GetHeights2DD(), RegionInfo.RegionID);

                    SendTerrainUpdate(true);

                    Terrain.ResetTaint();
                }
            }
        }

        public void SendTerrainUpdate(bool checkForTainted)
        {
            float[] terData = Terrain.GetHeights1D();

            Broadcast(delegate(IClientAPI client)
            {
                for (int x = 0; x < 16; x++)
                {
                    for (int y = 0; y < 16; y++)
                    {
                        if ((!checkForTainted) || (Terrain.IsTainted(x * 16, y * 16)))
                        {
                            client.SendLayerData(x, y, terData);
                        }
                    }
                }
            });
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
                CommsManager.GridService.RequestNeighbourMapBlocks((int) (RegionInfo.RegionLocX - 9),
                                                                   (int) (RegionInfo.RegionLocY - 9),
                                                                   (int) (RegionInfo.RegionLocX + 9),
                                                                   (int) (RegionInfo.RegionLocY + 9));
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
                ushort x = (ushort) ((mapBlocks[i].X - RegionInfo.RegionLocX) + 10);
                ushort y = (ushort) ((mapBlocks[i].Y - RegionInfo.RegionLocY) + 10);
                g.DrawImage(bitImages[i], (x*128), (y*128), 128, 128);
            }
            mapTexture.Save(fileName, ImageFormat.Jpeg);
        }

        /// <summary>
        /// Loads a world map from a specified R32 file
        /// </summary>
        /// <param name="filename">A working R32 file</param>
        public void LoadWorldMap(string filename)
        {
            Terrain.LoadFromFileF32(filename);
            Terrain.SaveRevertMap();
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
                    if (string.IsNullOrEmpty(m_regInfo.EstateSettings.terrainFile))
                    {
                        m_log.Info("[TERRAIN]: No default terrain. Generating a new terrain.");
                        Terrain.SetDefaultTerrain();

                        m_storageManager.DataStore.StoreTerrain(Terrain.GetHeights2DD(), RegionInfo.RegionID);
                    }
                    else
                    {
                        try
                        {
                            Terrain.LoadFromFileF32(m_regInfo.EstateSettings.terrainFile);
                            Terrain *= m_regInfo.EstateSettings.terrainMultiplier;
                        }
                        catch
                        {
                            m_log.Info("[TERRAIN]: No terrain found in database or default. Generating a new terrain.");
                            Terrain.SetDefaultTerrain();
                        }
                        m_storageManager.DataStore.StoreTerrain(Terrain.GetHeights2DD(), RegionInfo.RegionID);
                    }
                }
                else
                {
                    Terrain.SetHeights2D(map);
                }

                CreateTerrainTexture(true);
                //CommsManager.GridService.RegisterRegion(RegionInfo); //hack to update the terrain texture in grid mode so it shows on world map
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
                    m_LandManager.allowedForcefulBans = false;
                }
                else
                {
                    m_log.Info("[GRID]: Grid is allowing forceful parcel banlists");
                    m_LandManager.allowedForcefulBans = true;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void CreateTerrainTexture(bool temporary)
        {
            //create a texture asset of the terrain 
            byte[] data = Terrain.WriteJpegImage("defaultstripe.png");
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

        #endregion

        #region Load Land

        public void loadAllLandObjectsFromStorage()
        {
                m_log.Info("[SCENE]: Loading land objects from storage");
                List<LandData> landData = m_storageManager.DataStore.LoadLandObjects(RegionInfo.RegionID);

                if (landData.Count == 0)
                {
                    m_LandManager.NoLandDataFromStorage();
                }
                else
                {
                    m_LandManager.IncomingLandObjectsFromStorage(landData);
                }
        }

        #endregion

        #region Primitives Methods

        /// <summary>
        /// Loads the World's objects
        /// </summary>
        public virtual void LoadPrimsFromStorage(bool m_permissions)
        {
            m_log.Info("[SCENE]: Loading objects from datastore");
            
            List<SceneObjectGroup> PrimsFromDB = m_storageManager.DataStore.LoadObjects(m_regInfo.RegionID);
            foreach (SceneObjectGroup group in PrimsFromDB)
            {
                AddEntityFromStorage(group);
                SceneObjectPart rootPart = group.GetChildPart(group.UUID);
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
                if (target != null)
                {
                    pos = target.AbsolutePosition;

                    //m_log.Info("[RAYTRACE]: " + pos.ToString());
                    //EntityIntersection rayTracing = null;
                    //ScenePresence presence = ((ScenePresence)GetScenePresence(ownerID));
                    //if (presence != null)
                    //{
                        //Vector3 CameraPosition = presence.CameraPosition;
                        //Vector3 rayEnd = new Vector3(pos.X, pos.Y, pos.Z);

                        //float rayMag = m_innerScene.Vector3Distance(CameraPosition, rayEnd);
                        //LLVector3 rayDirectionLL = Util.GetNormal(pos);

                        //Vector3 rayDirection = new Vector3(rayDirectionLL.X, rayDirectionLL.Y, rayDirectionLL.Z);

                        //Ray rezRay = new Ray(CameraPosition, rayDirection);

                        //Vector3 RezDirectionFromCamera = rezRay.Direction;

                        //rayTracing = m_innerScene.GetClosestIntersectingPrim(rezRay);
                    //}

                    //if ((rayTracing != null) && (rayTracing.HitTF))
                    //{
                        // We raytraced and found a prim in the way of the ground..  so 
                        // We will rez the object somewhere close to the prim.  Better math needed. This is a Stub
                        //Vector3 Newpos = new Vector3(rayTracing.obj.AbsolutePosition.X,rayTracing.obj.AbsolutePosition.Y,rayTracing.obj.AbsolutePosition.Z);
                        //Vector3 Newpos = rayTracing.ipoint;
                        //Vector3 NewScale =
                            //new Vector3(rayTracing.obj.Scale.X, rayTracing.obj.Scale.Y, rayTracing.obj.Scale.Z);

                        //Quaternion ParentRot = rayTracing.obj.ParentGroup.Rotation;
                        //Quaternion ParentRot = new Quaternion(primParentRot.W,primParentRot.X,primParentRot.Y,primParentRot.Z);

                        //LLQuaternion primLocalRot = rayTracing.obj.RotationOffset;
                        //Quaternion LocalRot = new Quaternion(primLocalRot.W, primLocalRot.X, primLocalRot.Y, primLocalRot.Z);

                        //Quaternion NewRot = LocalRot * ParentRot;

                        //Vector3 RezPoint = Newpos;

                        //m_log.Info("[REZINFO]: Possible Rez Point:" + RezPoint.ToString());
                        //pos = new LLVector3(RezPoint.x, RezPoint.y, RezPoint.z);
                    //}

                    return pos;
                }
                else
                {
                    // fall back to our stupid functionality
                    pos = RayEnd;
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
            if ((rootPart.Shape.PCode == 95) || (rootPart.Shape.PCode == 255) || (rootPart.Shape.PCode == 111))
            {
                rootPart.AddFlag(LLObject.ObjectFlags.Phantom);
                //rootPart.ObjectFlags += (uint)LLObject.ObjectFlags.Phantom;
            }
            // if not phantom, add to physics
            sceneOb.ApplyPhysics(m_physicalPrim);

            return sceneOb;
        }

        public SceneObjectGroup AddTree(LLVector3 scale, LLQuaternion rotation, LLVector3 position,
                            Tree treeType, bool newTree)
        {
            LLUUID uuid = this.RegionInfo.MasterAvatarAssignedUUID;
            PrimitiveBaseShape treeShape = new PrimitiveBaseShape();
            treeShape.PathCurve = 16;
            treeShape.PathEnd = 49900;
            treeShape.PCode = newTree ? (byte) PCode.NewTree : (byte) PCode.Tree;
            treeShape.Scale = scale;
            treeShape.State = (byte) treeType;
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
                m_LandManager.removePrimFromLandPrimCounts(sceneObject);
                Entities.Remove(sceneObject.UUID);
                m_LandManager.setPrimsTainted();
                m_innerScene.RemoveAPrimCount();
            }
        }

        /// <summary>
        /// Called by a prim when it has been created/cloned, so that its events can be subscribed to
        /// </summary>
        /// <param name="prim"></param>
        public void AcknowledgeNewPrim(SceneObjectGroup prim)
        {
            prim.OnPrimCountTainted += m_LandManager.setPrimsTainted;
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

        #endregion

        #region Add/Remove Avatar Methods

        /// <summary>
        /// 
        /// </summary>
        /// <param name="client"></param          
        /// <param name="child"></param>
        public override void AddNewClient(IClientAPI client, bool child)
        {
            SubscribeToClientEvents(client);

            m_estateManager.sendRegionHandshake(client);

            CreateAndAddScenePresence(client, child);

            m_LandManager.sendParcelOverlay(client);
            CommsManager.UserProfileCacheService.AddNewUser(client.AgentId);
            CommsManager.TransactionsManager.AddUser(client.AgentId);
        }

        protected virtual void SubscribeToClientEvents(IClientAPI client)
        {
            client.OnRegionHandShakeReply += SendLayerData;
            //remoteClient.OnRequestWearables += new GenericCall(this.GetInitialPrims);
            client.OnModifyTerrain += ModifyTerrain;
            // client.OnRequestWearables += InformClientOfNeighbours;
            client.OnAddPrim += AddNewPrim;
            client.OnUpdatePrimGroupPosition += m_innerScene.UpdatePrimPosition;
            client.OnUpdatePrimSinglePosition += m_innerScene.UpdatePrimSinglePosition;
            client.OnUpdatePrimGroupRotation += m_innerScene.UpdatePrimRotation;
            client.OnUpdatePrimGroupMouseRotation += m_innerScene.UpdatePrimRotation;
            client.OnUpdatePrimSingleRotation += m_innerScene.UpdatePrimSingleRotation;
            client.OnUpdatePrimScale += m_innerScene.UpdatePrimScale;
            client.OnUpdateExtraParams += m_innerScene.UpdateExtraParam;
            client.OnUpdatePrimShape += m_innerScene.UpdatePrimShape;
            client.OnRequestMapBlocks += RequestMapBlocks;
            client.OnUpdatePrimTexture += m_innerScene.UpdatePrimTexture;
            client.OnTeleportLocationRequest += RequestTeleportLocation;
            client.OnObjectSelect += SelectPrim;
            client.OnObjectDeselect += DeselectPrim;
            client.OnGrabUpdate += m_innerScene.MoveObject;
            client.OnDeRezObject += DeRezObject;
            client.OnRezObject += RezObject;
            client.OnNameFromUUIDRequest += CommsManager.HandleUUIDNameRequest;
            client.OnObjectDescription += m_innerScene.PrimDescription;
            client.OnObjectName += m_innerScene.PrimName;
            client.OnLinkObjects += m_innerScene.LinkObjects;
            client.OnDelinkObjects += m_innerScene.DelinkObjects;
            client.OnObjectDuplicate += m_innerScene.DuplicateObject;
            client.OnUpdatePrimFlags += m_innerScene.UpdatePrimFlags;
            client.OnRequestObjectPropertiesFamily += m_innerScene.RequestObjectPropertiesFamily;
            client.OnParcelPropertiesRequest += new ParcelPropertiesRequest(m_LandManager.handleParcelPropertiesRequest);
            client.OnParcelDivideRequest += new ParcelDivideRequest(m_LandManager.handleParcelDivideRequest);
            client.OnParcelJoinRequest += new ParcelJoinRequest(m_LandManager.handleParcelJoinRequest);
            client.OnParcelPropertiesUpdateRequest +=
                new ParcelPropertiesUpdateRequest(m_LandManager.handleParcelPropertiesUpdateRequest);
            client.OnParcelSelectObjects += new ParcelSelectObjects(m_LandManager.handleParcelSelectObjectsRequest);
            client.OnParcelObjectOwnerRequest +=
                new ParcelObjectOwnerRequest(m_LandManager.handleParcelObjectOwnersRequest);
            client.OnParcelAccessListRequest += new ParcelAccessListRequest(m_LandManager.handleParcelAccessRequest);
            client.OnParcelAccessListUpdateRequest +=
                new ParcelAccessListUpdateRequest(m_LandManager.handleParcelAccessUpdateRequest);

            client.OnEstateOwnerMessage += new EstateOwnerMessageRequest(m_estateManager.handleEstateOwnerMessage);
            client.OnRegionInfoRequest += m_estateManager.HandleRegionInfoRequest;
            client.OnEstateCovenantRequest += m_estateManager.HandleEstateCovenantRequest;
            client.OnRequestGodlikePowers += handleRequestGodlikePowers;
            client.OnGodKickUser += handleGodlikeKickUser;
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
            client.OnAssetUploadRequest += CommsManager.TransactionsManager.HandleUDPUploadRequest;
            client.OnXferReceive += CommsManager.TransactionsManager.HandleXfer;
            client.OnRezScript += RezScript;

            client.OnRequestTaskInventory += RequestTaskInventory;
            client.OnRemoveTaskItem += RemoveTaskInventory;
            client.OnUpdateTaskInventory += UpdateTaskInventory;

            client.OnGrabObject += ProcessObjectGrab;
            client.OnMoneyTransferRequest += ProcessMoneyTransferRequest;
            client.OnAvatarPickerRequest += ProcessAvatarPickerRequest;
            client.OnPacketStats += AddPacketStats;

            EventManager.TriggerOnNewClient(client);
        }

        protected virtual ScenePresence CreateAndAddScenePresence(IClientAPI client, bool child)
        {
            ScenePresence avatar = null;

            AvatarAppearance appearance;
            GetAvatarAppearance(client, out appearance);

            avatar = m_innerScene.CreateAndAddScenePresence(client, child, appearance);

            if (avatar.IsChildAgent)
            {
                avatar.OnSignificantClientMovement += m_LandManager.handleSignificantClientMovement;
            }

            return avatar;
        }


        protected void GetAvatarAppearance(IClientAPI client, out AvatarAppearance appearance)
        {
            if (m_AvatarFactory == null ||
                !m_AvatarFactory.TryGetAvatarAppearance(client.AgentId, out appearance))
            {
                //not found Appearance
                byte[] visualParams;
                AvatarWearable[] wearables;
                AvatarFactoryModule.GetDefaultAvatarAppearance(out wearables, out visualParams);
                appearance = new AvatarAppearance(client.AgentId, wearables, visualParams);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="agentID"></param>
        public override void RemoveClient(LLUUID agentID)
        {
            ScenePresence avatar = GetScenePresence(agentID);
            try
            {
                if (avatar.IsChildAgent)
                {
                    m_innerScene.removeUserCount(false);
                }
                else
                {
                    m_innerScene.removeUserCount(true);
                    CommsManager.LogOffUser(agentID, RegionInfo.RegionID, RegionInfo.RegionHandle, 
                                                avatar.AbsolutePosition.X, avatar.AbsolutePosition.Y, 
                                                avatar.AbsolutePosition.Z);
                    m_sceneGridService.SendCloseChildAgentConnections(avatar);
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
                              catch (System.NullReferenceException)
                              {
                                  //We can safely ignore null reference exceptions.  It means the avatar are dead and cleaned up anyway.
                              }
                          });

            ForEachScenePresence(
                delegate(ScenePresence presence) { presence.CoarseLocationChange(); });

            lock (m_scenePresences)
            {
                m_scenePresences.Remove(agentID);
            }

            lock (Entities)
            {
                Entities.Remove(agentID);
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
                m_log.Error("Scene.cs:RemoveClient exception: " + e.ToString());
            }

            // Remove client agent from profile, so new logins will work
            CommsManager.UserService.clearUserAgent(agentID);
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
           

            


            m_sceneGridService.KillObject = SendKillObject;
        }

        /// <summary>
        /// 
        /// </summary>
        public void UnRegisterReginWithComms()
        {
            m_sceneGridService.OnChildAgentUpdate -= IncomingChildAgentDataUpdate;
            m_sceneGridService.OnRegionUp -= OtherRegionUp;
            m_sceneGridService.OnExpectUser -= NewUserConnection;
            m_sceneGridService.OnAvatarCrossingIntoRegion -= AgentCrossing;
            m_sceneGridService.OnCloseAgentConnection -= CloseConnection;

            m_sceneGridService.Close();
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="agent"></param>
        public void NewUserConnection(ulong regionHandle, AgentCircuitData agent)
        {
            if (regionHandle == m_regInfo.RegionHandle)
            {
                if (agent.CapsPath != String.Empty)
                {
                    Caps cap =
                        new Caps(AssetCache, httpListener, m_regInfo.ExternalHostName, httpListener.Port,
                                 agent.CapsPath, agent.AgentID, m_dumpAssetsToFile);

                    Util.SetCapsURL(agent.AgentID,
                                    "http://" + m_regInfo.ExternalHostName + ":" + httpListener.Port.ToString() +
                                    "/CAPS/" + agent.CapsPath + "0000/");
                    cap.RegisterHandlers();
                    if (agent.child)
                    {
                    
                    }
                    cap.AddNewInventoryItem = AddInventoryItem;
                    cap.ItemUpdatedCall = CapsUpdateInventoryItemAsset;
                    cap.TaskScriptUpdatedCall = CapsUpdateTaskInventoryScriptAsset;

                    if (m_capsHandlers.ContainsKey(agent.AgentID))
                    {
                        //m_log.Warn("[client]: Adding duplicate CAPS entry for user " +
                        //    agent.AgentID.ToString());
                        try
                        {
                            m_capsHandlers[agent.AgentID] = cap;
                        }
                        catch (KeyNotFoundException)
                        {
                            // Fix for a potential race condition.
                            m_capsHandlers.Add(agent.AgentID, cap);
                        }
                    }
                    else
                    {
                        m_capsHandlers.Add(agent.AgentID, cap);
                    }
                }
                m_authenticateHandler.AddNewCircuit(agent.circuitcode, agent);
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
                    libsecondlife.Packets.DisableSimulatorPacket disable = (libsecondlife.Packets.DisableSimulatorPacket) PacketPool.Instance.GetPacket(libsecondlife.Packets.PacketType.DisableSimulator);
                    presence.ControllingClient.OutPacket(disable, ThrottleOutPacketType.Task);
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="mod"></param>
        public void RegisterModuleInterface<M>(M mod)
        {
            if (!ModuleInterfaces.ContainsKey(typeof (M)))
            {
                ModuleInterfaces.Add(typeof (M), mod);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public T RequestModuleInterface<T>()
        {
            if (ModuleInterfaces.ContainsKey(typeof (T)))
            {
                return (T) ModuleInterfaces[typeof (T)];
            }
            else
            {
                return default(T);
            }
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

        public void SendDialogToUser(LLUUID avatarID, string objectName, LLUUID objectID, LLUUID ownerID,string message,LLUUID TextureID,int ch,string[] buttonlabels)
        {
            if (m_scenePresences.ContainsKey(avatarID))
            {
                m_scenePresences[avatarID].ControllingClient.SendDialog(objectName,objectID,ownerID,message,TextureID,ch,buttonlabels);
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
        /// 
        /// </summary>
        public void performParcelPrimCountUpdate()
        {
            m_LandManager.resetAllLandPrimCounts();
            m_eventManager.TriggerParcelPrimCountUpdate();
            m_LandManager.finalizeLandPrimCountUpdate();
                m_LandManager.landPrimCountTainted = false;
        }

        /// <summary>
        /// 
        /// </summary>
        public void addPrimsToParcelCounts()
        {
            foreach (EntityBase obj in Entities.Values)
            {
                if (obj is SceneObjectGroup)
                {
                    m_eventManager.TriggerParcelPrimCountAdd((SceneObjectGroup) obj);
                }
            }
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
        public void TriggerGridInstantMessage(GridInstantMessage message,InstantMessageReceiver options)
        {
            m_eventManager.TriggerGridInstantMessage(message,options);
        }


        public virtual void StoreAddFriendship(LLUUID ownerID, LLUUID friendID, uint perms)
        {
            // TODO: m_sceneGridService.DoStuff;
            CommsManager.AddNewUserFriend(ownerID, friendID, perms);
        }

        public virtual void StoreUpdateFriendship(LLUUID ownerID, LLUUID friendID, uint perms)
        {
            // TODO: m_sceneGridService.DoStuff;
            CommsManager.UpdateUserFriendPerms(ownerID, friendID, perms);
        }

        public virtual void StoreRemoveFriendship(LLUUID ownerID, LLUUID ExfriendID)
        {
            // TODO: m_sceneGridService.DoStuff;
            CommsManager.RemoveUserFriend(ownerID, ExfriendID);
        }
        public virtual List<FriendListItem> StoreGetFriendsForUser(LLUUID ownerID)
        {
            // TODO: m_sceneGridService.DoStuff;
            return CommsManager.GetUserFriendList(ownerID);
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
        public void handleRequestGodlikePowers(LLUUID agentID, LLUUID sessionID, LLUUID token,
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
                            m_scenePresences[agentID].GrantGodlikePowers(agentID, testSessionID, token);
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
        public void handleGodlikeKickUser(LLUUID godID, LLUUID sessionID, LLUUID agentID, uint kickflags, byte[] reason)
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="controller"></param>
        /// <param name="agentID"></param>
        /// <param name="sessionID"></param>
        /// <param name="permChanges"></param>
        public void HandleObjectPermissionsUpdate(IClientAPI controller, LLUUID agentID, LLUUID sessionID,
                                                  List<ObjectPermissionsPacket.ObjectDataBlock> permChanges)
        {
            // Check for spoofing..  since this is permissions we're talking about here!
            if ((controller.SessionId == sessionID) && (controller.AgentId == agentID))
            {
                for (int i = 0; i < permChanges.Count; i++)
                {
                    // Tell the object to do permission update
                    byte field = permChanges[i].Field;
                    uint localID = permChanges[i].ObjectLocalID;
                    uint mask = permChanges[i].Mask;
                    byte addRemTF = permChanges[i].Set;
                    SceneObjectGroup chObjectGroup = GetGroupByPrim(localID);
                    chObjectGroup.UpdatePermissions(agentID, field, localID, mask, addRemTF);
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
                    ((SceneObjectGroup) ent).ScheduleGroupForFullUpdate();
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
                    SceneObjectPart part = ((SceneObjectGroup) ent).GetChildPart(((SceneObjectGroup) ent).UUID);
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
                    m_log.Error(
                        String.Format("{0,-16}{1,-16}{2,-25}{3,-25}{4,-16}{5,-16}{6,-16}", "Firstname", "Lastname",
                                      "Agent ID", "Session ID", "Circuit", "IP", "World"));

                    foreach (ScenePresence scenePrescence in GetAvatars())
                    {
                        m_log.Error(
                            String.Format("{0,-16}{1,-16}{2,-25}{3,-25}{4,-16},{5,-16}{6,-16}",
                                          scenePrescence.Firstname,
                                          scenePrescence.Lastname,
                                          scenePrescence.UUID,
                                          scenePrescence.ControllingClient.AgentId,
                                          "Unknown",
                                          "Unknown",
                                          RegionInfo.RegionName));
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
            return Terrain.GetHeight(x, y);
        }

        public LLUUID GetLandOwner(float x, float y)
        {
            Land land = LandManager.getLandObject(x, y);
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
            return LandManager.getLandObject(x, y).landData;
        }

        public void SetLandMusicURL(float x, float y, string url)
        {
            Land land = LandManager.getLandObject(x, y);
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
    }
}
