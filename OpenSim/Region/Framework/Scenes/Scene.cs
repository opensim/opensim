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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Timers;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Framework.Monitoring;
using OpenSim.Services.Interfaces;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes.Serialization;
using OpenSim.Region.PhysicsModules.SharedBase;
using Timer = System.Timers.Timer;
using TPFlags = OpenSim.Framework.Constants.TeleportFlags;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;
using PermissionMask = OpenSim.Framework.PermissionMask;

namespace OpenSim.Region.Framework.Scenes
{
    public delegate bool FilterAvatarList(ScenePresence avatar);

    public partial class Scene : SceneBase
    {
        private const long DEFAULT_MIN_TIME_FOR_PERSISTENCE = 60L;
        private const long DEFAULT_MAX_TIME_FOR_PERSISTENCE = 600L;

        public delegate void SynchronizeSceneHandler(Scene scene);


        #region Fields

        /// <summary>
        /// Show debug information about animations.
        /// </summary>
        public bool DebugAnimations { get; set; }

        /// <summary>
        /// Show debug information about teleports.
        /// </summary>
        public bool DebugTeleporting { get; set; }

        /// <summary>
        /// Show debug information about the scene loop.
        /// </summary>
        public bool DebugUpdates { get; set; }

        /// <summary>
        /// If true then the scene is saved to persistent storage periodically, every m_update_backup frames and
        /// if objects meet required conditions (m_dontPersistBefore and m_dontPersistAfter).
        /// </summary>
        /// <remarks>
        /// Even if false, the scene will still be saved on clean shutdown.
        /// FIXME: Currently, setting this to false will mean that objects are not periodically returned from parcels.
        /// This needs to be fixed.
        /// </remarks>
        public bool PeriodicBackup { get; set; }

        /// <summary>
        /// If false then the scene is never saved to persistence storage even if PeriodicBackup == true and even
        /// if the scene is being shut down for the final time.
        /// </summary>
        public bool UseBackup { get; set; }

        /// <summary>
        /// If false then physical objects are disabled, though collisions will continue as normal.
        /// </summary>

        public bool PhysicsEnabled
        {
            get
            {
                return m_physicsEnabled;
            }

            set
            {
                m_physicsEnabled = value;

                if (PhysicsScene is IPhysicsParameters parameters)
                {
                     parameters.SetPhysicsParameter(
                            "Active", m_physicsEnabled.ToString(), PhysParameterEntry.APPLY_TO_NONE);
                }
            }
        }

        private bool m_physicsEnabled;

        /// <summary>
        /// If false then scripts are not enabled on the simulator
        /// </summary>
        public bool ScriptsEnabled
        {
            get { return m_scripts_enabled; }
            set
            {
                if (m_scripts_enabled != value)
                {
                    if (!value)
                    {
                        m_log.Info("Stopping all Scripts in Scene");

                        EntityBase[] entities = Entities.GetEntities();
                        foreach (EntityBase ent in entities)
                        {
                            if (ent is SceneObjectGroup group)
                                group.RemoveScriptInstances(false);
                        }
                    }
                    else
                    {
                        m_log.Info("Starting all Scripts in Scene");

                        EntityBase[] entities = Entities.GetEntities();
                        foreach (EntityBase ent in entities)
                        {
                            if (ent is SceneObjectGroup sog)
                            {
                                sog.CreateScriptInstances(0, false, DefaultScriptEngine, 0);
                                sog.ResumeScripts();
                            }
                        }
                    }

                    m_scripts_enabled = value;
                }
            }
        }
        private bool m_scripts_enabled;

        /// <summary>
        /// Used to prevent simultaneous calls to code that adds and removes agents.
        /// </summary>
        private readonly object m_removeClientLock = new();

        /// <summary>
        /// Statistical information for this scene.
        /// </summary>
        public SimStatsReporter StatsReporter { get; private set; }

        /// <summary>
        /// Controls whether physics can be applied to prims.  Even if false, prims still have entries in a
        /// PhysicsScene in order to perform collision detection
        /// </summary>
        public bool PhysicalPrims { get; private set; }

        /// <summary>
        /// Controls whether prims can be collided with.
        /// </summary>
        /// <remarks>
        /// If this is set to false then prims cannot be subject to physics either.
        /// </summary>
        public bool CollidablePrims { get; private set; }

        /// <summary>
        /// Minimum value of the size of a non-physical prim in each axis
        /// </summary>
        public float m_minNonphys = 0.001f;

        /// <summary>
        /// Maximum value of the size of a non-physical prim in each axis
        /// </summary>
        public float m_maxNonphys = 256;

        /// <summary>
        /// Minimum value of the size of a physical prim in each axis
        /// </summary>
        public float m_minPhys = 0.01f;

        /// <summary>
        /// Maximum value of the size of a physical prim in each axis
        /// </summary>
        public float m_maxPhys = 10;

        /// <summary>
        /// Max prims an object will hold
        /// </summary>
        public int m_linksetCapacity = 0;

        public bool m_clampPrimSize;
        public bool m_trustBinaries;
        public bool m_allowScriptCrossings = true;

        /// <summary>
        /// use legacy sittarget offsets to avoid contents breaks
        /// to compensate for SL bug
        /// </summary>
        public bool LegacySitOffsets = true;

        /// <summary>
        /// Can avatars cross from and to this region?
        /// </summary>
        public bool AllowAvatarCrossing { get; set; }

        /// Max prims an Physical object will hold
        /// </summary>
        ///
        public int m_linksetPhysCapacity = 0;

        public int m_LinkSetDataLimit = 32 * 1024;

        /// <summary>
        /// When placed outside the region's border, do we transfer the objects or
        /// do we keep simulating them here?
        /// </summary>
        public bool DisableObjectTransfer { get; set; }

        public bool m_useFlySlow;
        public bool m_useTrashOnDelete = true;

         protected float m_defaultDrawDistance = 255f;
        protected float m_defaultCullingDrawDistance = 16f;
        public float DefaultDrawDistance
        {
             get { return ObjectsCullingByDistance?m_defaultCullingDrawDistance:m_defaultDrawDistance; }
        }

        protected float m_maxDrawDistance = 512.0f;
//        protected float m_maxDrawDistance = 256.0f;
        public float MaxDrawDistance
        {
            get { return m_maxDrawDistance; }
        }

        protected float m_maxRegionViewDistance = 255f;
        public float MaxRegionViewDistance
        {
            get { return m_maxRegionViewDistance; }
        }

        protected float m_minRegionViewDistance = 96f;
        public float MinRegionViewDistance
        {
            get { return m_minRegionViewDistance; }
        }

        private readonly List<string> m_AllowedViewers = new();
        private readonly List<string> m_BannedViewers = new();

        // TODO: need to figure out how allow client agents but deny
        // root agents when ACL denies access to root agent
        public bool m_strictAccessControl = true;
        public bool m_seeIntoBannedRegion = false;
        public int MaxUndoCount = 5;

        public bool SeeIntoRegion { get; set; }

        // Using this for RegionReady module to prevent LoginsDisabled from changing under our feet;
        public bool LoginLock = false;

        public bool StartDisabled = false;
        public bool LoadingPrims;
        public IXfer XferManager;

        // the minimum time that must elapse before a changed object will be considered for persisted
        public long m_dontPersistBefore = DEFAULT_MIN_TIME_FOR_PERSISTENCE * 10000000L;
        // the maximum time that must elapse before a changed object will be considered for persisted
        public long m_persistAfter = DEFAULT_MAX_TIME_FOR_PERSISTENCE * 10000000L;

        protected int m_splitRegionID;
        protected Timer m_restartWaitTimer = new();
        protected Timer m_timerWatchdog = new();
        protected List<RegionInfo> m_regionRestartNotifyList = new();
        protected List<RegionInfo> m_neighbours = new();
        protected string m_simulatorVersion = "OpenSimulator Server";
        protected AgentCircuitManager m_authenticateHandler;
        protected SceneCommunicationService m_sceneGridService;
        protected ISnmpModule m_snmpService = null;

        protected ISimulationDataService m_SimulationDataService;
        protected IEstateDataService m_EstateDataService;
        protected IAssetService m_AssetService;
        protected IAuthorizationService m_AuthorizationService;
        protected IInventoryService m_InventoryService;
        protected IGridService m_GridService;
        protected ILibraryService m_LibraryService;
        protected ISimulationService m_simulationService;
        protected IAuthenticationService m_AuthenticationService;
        protected IPresenceService m_PresenceService;
        protected IUserAccountService m_UserAccountService;
        protected IAvatarService m_AvatarService;
        protected IGridUserService m_GridUserService;
        protected IAgentPreferencesService m_AgentPreferencesService;

        protected IXMLRPC m_xmlrpcModule;
        protected IWorldComm m_worldCommModule;
        protected IAvatarFactoryModule m_AvatarFactory;
        protected IConfigSource m_config;
        protected IRegionSerialiserModule m_serialiser;
        protected IDialogModule m_dialogModule;
        protected ICapabilitiesModule m_capsModule;
        protected IGroupsModule m_groupsModule;

        private readonly Dictionary<string, string> m_extraSettings;

        /// <summary>
        /// Current scene frame number
        /// </summary>
        public uint Frame
        {
            get;
            protected set;
        }

        /// <summary>
        /// Frame time
        /// </remarks>
        public float FrameTime { get; private set; }
        public int FrameTimeWarnPercent { get; private set; }
        public int FrameTimeCritPercent { get; private set; }

        // Normalize the frame related stats to nominal 55fps for viewer and scripts option
        // see SimStatsReporter.cs
        public bool Normalized55FPS { get; private set; }

        private readonly int m_update_physics = 1;
        private readonly int m_update_entitymovement = 1;
        private readonly int m_update_objects = 1;
        private readonly int m_update_presences = 1; // Update scene presence movements
        private readonly int m_update_events = 1;
        private readonly int m_update_backup = 200;

        private readonly int m_update_terrain = 1000;

        private readonly int m_update_coarse_locations = 5;
        private readonly int m_update_temp_cleaning = 180;

        private float agentMS;
        private float frameMS;
        private float physicsMS2;
        private float physicsMS;
        private float otherMS;
        private float tempOnRezMS;
        private float eventMS;
        private float backupMS;
        private float terrainMS;
        private float landMS;

        /// <summary>
        /// Tick at which the last frame was processed.
        /// </summary>
        private int m_lastFrameTick;

        /// <summary>
        /// Total script execution time (in Stopwatch Ticks) since the last frame
        /// </summary>
        private long m_scriptExecutionTime = 0;

        /// <summary>
        /// Signals whether temporary objects are currently being cleaned up.  Needed because this is launched
        /// asynchronously from the update loop.
        /// </summary>
        private bool m_cleaningTemps = false;
        private bool m_sendingCoarseLocations = false; // same for async course locations sending

        // TODO: Possibly stop other classes being able to manipulate this directly.
        private readonly SceneGraph m_sceneGraph;
        private readonly Timer m_restartTimer = new(15000); // Wait before firing
        private volatile bool m_backingup;
        private readonly Dictionary<UUID, ReturnInfo> m_returns = new();
        private readonly HashSet<UUID> m_groupsWithTargets = new();

        private readonly string m_defaultScriptEngine;

        private int m_unixStartTime;
        public int UnixStartTime
        {
            get { return m_unixStartTime; }
        }

        /// <summary>
        /// Tick at which the last login occurred.
        /// </summary>
        private int m_LastLogin;

        private int m_lastIncoming;
        private int m_lastOutgoing;
        private int m_hbRestarts = 0;


        /// <summary>
        /// Thread that runs the scene loop.
        /// </summary>
        private Thread m_heartbeatThread;

        /// <summary>
        /// True if these scene is in the process of shutting down or is shutdown.
        /// </summary>
        public bool ShuttingDown
        {
            get { return m_shuttingDown; }
        }
        private volatile bool m_shuttingDown;

        /// <summary>
        /// Is the scene active?
        /// </summary>
        /// <remarks>
        /// If false, update loop is not being run, though after setting to false update may still
        /// be active for a period (and IsRunning will still be true).  Updates can still be triggered manually if
        /// the scene is not active.
        /// </remarks>
        public bool Active
        {
            get { return m_active; }
            set
            {
                if (value)
                {
                    if (!m_active)
                        Start(false);
                }
                else
                {
                    // This appears assymetric with Start() above but is not - setting m_active = false stops the loops
                    // XXX: Possibly this should be in an explicit Stop() method for symmetry.
                    m_active = false;
                }
            }
        }
        private volatile bool m_active;

        /// <summary>
        /// If true then updates are running.  This may be true for a short period after a scene is de-activated.
        /// </summary>
        public bool IsRunning
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return m_isRunning; }
        }
        private volatile bool m_isRunning;

        private bool m_firstHeartbeat = true;

//        private UpdatePrioritizationSchemes m_priorityScheme = UpdatePrioritizationSchemes.Time;
//        private bool m_reprioritizationEnabled = true;
//        private double m_reprioritizationInterval = 5000.0;
//        private double m_rootReprioritizationDistance = 10.0;
//        private double m_childReprioritizationDistance = 20.0;


        private readonly Timer m_mapGenerationTimer = new();
        private readonly bool m_generateMaptiles;

        protected int m_lastHealth = -1;
        protected int m_lastUsers = -1;

        #endregion Fields

        #region Properties

        /* Used by the loadbalancer plugin on GForge */
        public int SplitRegionID
        {
            get { return m_splitRegionID; }
            set { m_splitRegionID = value; }
        }

        public new float TimeDilation
        {
            get { return m_sceneGraph.PhysicsScene.TimeDilation; }
        }

        public void setThreadCount(int inUseThreads)
        {
            // Just pass the thread count information on its way as the Scene
            // does not require the value for anything at this time
            StatsReporter.SetThreadCount(inUseThreads);
        }

        public SceneCommunicationService SceneGridService
        {
            get { return m_sceneGridService; }
        }

        public ISnmpModule SnmpService
        {
            get
            {
                m_snmpService ??= RequestModuleInterface<ISnmpModule>();

                return m_snmpService;
            }
        }

        public ISimulationDataService SimulationDataService
        {
            get
            {
                if (m_SimulationDataService is null)
                {
                    m_SimulationDataService = RequestModuleInterface<ISimulationDataService>();

                    if (m_SimulationDataService is null)
                    {
                        throw new Exception("No ISimulationDataService available.");
                    }
                }

                return m_SimulationDataService;
            }
        }

        public IEstateDataService EstateDataService
        {
            get
            {
                if (m_EstateDataService is null)
                {
                    m_EstateDataService = EstateDataServiceSafe;

                    if (m_EstateDataService is null)
                    {
                        throw new Exception("No IEstateDataService available.");
                    }
                }

                return m_EstateDataService;
            }
        }

        /// <summary>
        /// Similar to 'EstateDataService', but if the service isn't found returns null instead of throwing an exception.
        /// </summary>
        public IEstateDataService EstateDataServiceSafe
        {
            get
            {
                m_EstateDataService ??= RequestModuleInterface<IEstateDataService>();

                return m_EstateDataService;
            }
        }

        public IAssetService AssetService
        {
            get
            {
                if (m_AssetService is null)
                {
                    m_AssetService = RequestModuleInterface<IAssetService>();

                    if (m_AssetService is null)
                    {
                        throw new Exception("No IAssetService available.");
                    }
                }

                return m_AssetService;
            }
        }

        public IAuthorizationService AuthorizationService
        {
            get
            {
                m_AuthorizationService ??= RequestModuleInterface<IAuthorizationService>();
                return m_AuthorizationService;
            }
        }

        public IInventoryService InventoryService
        {
            get
            {
                if (m_InventoryService is null)
                {
                    m_InventoryService = RequestModuleInterface<IInventoryService>();

                    if (m_InventoryService is null)
                    {
                        throw new Exception("No IInventoryService available. This could happen if the config_include folder doesn't exist or if the OpenSim.ini [Architecture] section isn't set.  Please also check that you have the correct version of your inventory service dll.  Sometimes old versions of this dll will still exist.  Do a clean checkout and re-create the opensim.ini from the opensim.ini.example.");
                    }
                }

                return m_InventoryService;
            }
        }

        public IGridService GridService
        {
            get
            {
                if (m_GridService is null)
                {
                    m_GridService = RequestModuleInterface<IGridService>();

                    if (m_GridService is null)
                    {
                        throw new Exception("No IGridService available. This could happen if the config_include folder doesn't exist or if the OpenSim.ini [Architecture] section isn't set.  Please also check that you have the correct version of your inventory service dll.  Sometimes old versions of this dll will still exist.  Do a clean checkout and re-create the opensim.ini from the opensim.ini.example.");
                    }
                }

                return m_GridService;
            }
        }

        public ILibraryService LibraryService
        {
            get
            {
                m_LibraryService ??= RequestModuleInterface<ILibraryService>();

                return m_LibraryService;
            }
        }

        public ISimulationService SimulationService
        {
            get
            {
                m_simulationService ??= RequestModuleInterface<ISimulationService>();

                return m_simulationService;
            }
        }

        public IAuthenticationService AuthenticationService
        {
            get
            {
                m_AuthenticationService ??= RequestModuleInterface<IAuthenticationService>();
                return m_AuthenticationService;
            }
        }

        public IPresenceService PresenceService
        {
            get
            {
                m_PresenceService ??= RequestModuleInterface<IPresenceService>();
                return m_PresenceService;
            }
        }

        public IUserAccountService UserAccountService
        {
            get
            {
                m_UserAccountService ??= RequestModuleInterface<IUserAccountService>();
                return m_UserAccountService;
            }
        }

        public IAvatarService AvatarService
        {
            get
            {
                m_AvatarService ??= RequestModuleInterface<IAvatarService>();
                return m_AvatarService;
            }
        }

        public IGridUserService GridUserService
        {
            get
            {
                m_GridUserService ??= RequestModuleInterface<IGridUserService>();
                return m_GridUserService;
            }
        }

        public IAgentPreferencesService AgentPreferencesService
        {
            get
            {
                m_AgentPreferencesService ??= RequestModuleInterface<IAgentPreferencesService>();
                return m_AgentPreferencesService;
            }
        }

        public IAttachmentsModule AttachmentsModule { get; set; }
        public IEntityTransferModule EntityTransferModule { get; private set; }
        public IAgentAssetTransactions AgentTransactionsModule { get; private set; }
        public IUserManagement UserManagementModule { get; private set; }

        public IAvatarFactoryModule AvatarFactory
        {
            get { return m_AvatarFactory; }
        }

        public ICapabilitiesModule CapsModule
        {
            get { return m_capsModule; }
        }

        public int MonitorFrameTime { get { return (int)frameMS; } }
        public int MonitorPhysicsUpdateTime { get { return (int)physicsMS; } }
        public int MonitorPhysicsSyncTime { get { return (int)physicsMS2; } }
        public int MonitorOtherTime { get { return (int)otherMS; } }
        public int MonitorTempOnRezTime { get { return (int)tempOnRezMS; } }
        public int MonitorEventTime { get { return (int)eventMS; } } // This may need to be divided into each event?
        public int MonitorBackupTime { get { return (int)backupMS; } }
        public int MonitorTerrainTime { get { return (int)terrainMS; } }
        public int MonitorLandTime { get { return (int)landMS; } }
        public int MonitorLastFrameTick { get { return m_lastFrameTick; } }

        public UpdatePrioritizationSchemes UpdatePrioritizationScheme { get; set; }
        public bool IsReprioritizationEnabled { get; set; }
        public float ReprioritizationInterval { get; set; }
        public float ReprioritizationDistance { get; set; }
        private readonly float m_minReprioritizationDistance = 32f;
        public bool ObjectsCullingByDistance = false;

        private readonly ExpiringCacheOS<UUID, UUID> TeleportTargetsCoolDown = new();

        public AgentCircuitManager AuthenticateHandler
        {
            get { return m_authenticateHandler; }
        }

        // an instance to the physics plugin's Scene object.
        public PhysicsScene PhysicsScene
        {
            get { return m_sceneGraph.PhysicsScene; }
            set
            {
                m_sceneGraph.PhysicsScene = value;
            }
        }

        public string DefaultScriptEngine
        {
            get { return m_defaultScriptEngine; }
        }

        public EntityManager Entities
        {
            get { return m_sceneGraph.Entities; }
        }


        // used in sequence see: SpawnPoint()
        private int m_SpawnPoint;
        // can be closest/random/sequence
        public string SpawnPointRouting
        {
            get;
            private set;
        }
        // allow landmarks to pass
        public bool TelehubAllowLandmarks
        {
            get;
            private set;
        }

        public GridInfo SceneGridInfo;

        #endregion Properties

        #region Constructors

        public Scene(RegionInfo regInfo, AgentCircuitManager authen,
                     ISimulationDataService simDataService, IEstateDataService estateDataService,
                     IConfigSource config, string simulatorVersion)
            : this(regInfo)
        {
            m_config = config;
            FrameTime = 0.0908f;
            FrameTimeWarnPercent = 60;
            FrameTimeCritPercent = 40;
            Normalized55FPS = true;
            SeeIntoRegion = true;

            m_lastAllocatedLocalId = (int)(Random.Shared.NextDouble() * (uint.MaxValue / 4));
            m_lastAllocatedIntId = (int)(Random.Shared.NextDouble() * (int.MaxValue / 4));
            m_authenticateHandler = authen;
            m_sceneGridService = new SceneCommunicationService();
            m_SimulationDataService = simDataService;
            m_EstateDataService = estateDataService;

            m_lastIncoming = 0;
            m_lastOutgoing = 0;

            m_asyncSceneObjectDeleter = new AsyncSceneObjectGroupDeleter(this)
            {
                Enabled = true
            };

            m_asyncInventorySender = new AsyncInventorySender(this);

            #region Region Settings

            // Load region settings
            // LoadRegionSettings creates new region settings in persistence if they don't already exist for this region.
            // However, in this case, the default textures are not set in memory properly, so we need to do it here and
            // resave.
            // FIXME: It shouldn't be up to the database plugins to create this data - we should do it when a new
            // region is set up and avoid these gyrations.
            RegionSettings rs = simDataService.LoadRegionSettings(RegionInfo.RegionID);
            m_extraSettings = simDataService.GetExtra(RegionInfo.RegionID);

            bool updatedTerrainTextures = false;
            if (rs.TerrainTexture1.IsZero())
            {
                rs.TerrainTexture1 = RegionSettings.DEFAULT_TERRAIN_TEXTURE_1;
                updatedTerrainTextures = true;
            }

            if (rs.TerrainTexture2.IsZero())
            {
                rs.TerrainTexture2 = RegionSettings.DEFAULT_TERRAIN_TEXTURE_2;
                updatedTerrainTextures = true;
            }

            if (rs.TerrainTexture3.IsZero())
            {
                rs.TerrainTexture3 = RegionSettings.DEFAULT_TERRAIN_TEXTURE_3;
                updatedTerrainTextures = true;
            }

            if (rs.TerrainTexture4.IsZero())
            {
                rs.TerrainTexture4 = RegionSettings.DEFAULT_TERRAIN_TEXTURE_4;
                updatedTerrainTextures = true;
            }

            if (rs.TerrainPBR1.IsZero() &&
                rs.TerrainPBR2.IsZero() &&
                rs.TerrainPBR3.IsZero() &&
                rs.TerrainPBR4.IsZero())
            {
                rs.TerrainPBR1 = rs.TerrainTexture1;
                rs.TerrainPBR2 = rs.TerrainTexture2;
                rs.TerrainPBR3 = rs.TerrainTexture3;
                rs.TerrainPBR4 = rs.TerrainTexture4;
                updatedTerrainTextures = true;
            }

            if (updatedTerrainTextures)
                rs.Save();

            RegionInfo.RegionSettings = rs;

            if (estateDataService is not null)
            {
                EstateSettings es = estateDataService.LoadEstateSettings(RegionInfo.RegionID, false);
                if (es == null)
                    m_log.Error($"[SCENE]: Region {Name} failed to load estate settings. Using defaults");
                RegionInfo.EstateSettings = es;
            }

            SceneGridInfo = new GridInfo(config, RegionInfo.ServerURI);

            #endregion Region Settings

            //Bind Storage Manager functions to some land manager functions for this scene
            EventManager.OnLandObjectAdded += new EventManager.LandObjectAdded(simDataService.StoreLandObject);
            EventManager.OnLandObjectRemoved += new EventManager.LandObjectRemoved(simDataService.RemoveLandObject);

             RegisterDefaultSceneEvents();

            // XXX: Don't set the public property since we don't want to activate here.  This needs to be handled
            // better in the future.
            m_scripts_enabled = !RegionInfo.RegionSettings.DisableScripts;

            PhysicsEnabled = !RegionInfo.RegionSettings.DisablePhysics;

            m_simulatorVersion = simulatorVersion + " (" + Util.RuntimeInformationStr + ")";

            #region Region Config

            // Region config overrides global config
            //
            if (m_config.Configs["Startup"] is not null)
            {
                IConfig startupConfig = m_config.Configs["Startup"];

                StartDisabled = startupConfig.GetBoolean("StartDisabled", false);

                m_defaultDrawDistance = startupConfig.GetFloat("DefaultDrawDistance", m_defaultDrawDistance);
                m_maxDrawDistance = startupConfig.GetFloat("MaxDrawDistance", m_maxDrawDistance);
                m_maxRegionViewDistance = startupConfig.GetFloat("MaxRegionsViewDistance", m_maxRegionViewDistance);
                m_minRegionViewDistance = startupConfig.GetFloat("MinRegionsViewDistance", m_minRegionViewDistance);

                // old versions compatibility
                LegacySitOffsets = startupConfig.GetBoolean("LegacySitOffsets", LegacySitOffsets);

                if (m_defaultDrawDistance > m_maxDrawDistance)
                    m_defaultDrawDistance = m_maxDrawDistance;

                if (m_maxRegionViewDistance > m_maxDrawDistance)
                    m_maxRegionViewDistance = m_maxDrawDistance;

                if(m_minRegionViewDistance < 96f)
                    m_minRegionViewDistance = 96f;
                if(m_minRegionViewDistance > m_maxRegionViewDistance)
                    m_minRegionViewDistance = m_maxRegionViewDistance;

                UseBackup = startupConfig.GetBoolean("UseSceneBackup", UseBackup);
                if (!UseBackup)
                    m_log.Info($"[SCENE]: Backup has been disabled for {RegionInfo.RegionName}");

                //Animation states
                m_useFlySlow = startupConfig.GetBoolean("enableflyslow", false);

                SeeIntoRegion = startupConfig.GetBoolean("see_into_region", SeeIntoRegion);

                MaxUndoCount = startupConfig.GetInt("MaxPrimUndos", 20);

                PhysicalPrims = startupConfig.GetBoolean("physical_prim", true);
                CollidablePrims = startupConfig.GetBoolean("collidable_prim", true);

                m_minNonphys = startupConfig.GetFloat("NonPhysicalPrimMin", m_minNonphys);
                if (RegionInfo.NonphysPrimMin > 0)
                {
                    m_minNonphys = RegionInfo.NonphysPrimMin;
                }
                // don't allow nonsense values
                if(m_minNonphys < 0.001f)
                    m_minNonphys = 0.001f;

                m_maxNonphys = startupConfig.GetFloat("NonPhysicalPrimMax", m_maxNonphys);
                if (RegionInfo.NonphysPrimMax > 0)
                {
                    m_maxNonphys = RegionInfo.NonphysPrimMax;
                }
                if (m_maxNonphys > 65536)
                    m_maxNonphys = 65536;

                m_minPhys = startupConfig.GetFloat("PhysicalPrimMin", m_minPhys);
                if (RegionInfo.PhysPrimMin > 0)
                {
                    m_minPhys = RegionInfo.PhysPrimMin;
                }
                if(m_minPhys < 0.01f)
                    m_minPhys = 0.01f;

                m_maxPhys = startupConfig.GetFloat("PhysicalPrimMax", m_maxPhys);

                if (RegionInfo.PhysPrimMax > 0)
                {
                    m_maxPhys = RegionInfo.PhysPrimMax;
                }
                if (m_maxPhys > 2048)
                    m_maxPhys = 2048;

                m_linksetCapacity = startupConfig.GetInt("LinksetPrims", m_linksetCapacity);
                if (RegionInfo.LinksetCapacity > 0)
                {
                    m_linksetCapacity = RegionInfo.LinksetCapacity;
                }

                m_linksetPhysCapacity = startupConfig.GetInt("LinksetPhysPrims", m_linksetPhysCapacity);


                SpawnPointRouting = startupConfig.GetString("SpawnPointRouting", "closest");
                TelehubAllowLandmarks = startupConfig.GetBoolean("TelehubAllowLandmark", false);

                // Here, if clamping is requested in either global or
                // local config, it will be used
                //
                m_clampPrimSize = startupConfig.GetBoolean("ClampPrimSize", m_clampPrimSize);
                if (RegionInfo.ClampPrimSize)
                {
                    m_clampPrimSize = true;
                }

                m_useTrashOnDelete = startupConfig.GetBoolean("UseTrashOnDelete",m_useTrashOnDelete);
                m_trustBinaries = startupConfig.GetBoolean("TrustBinaries", m_trustBinaries);
                m_allowScriptCrossings = startupConfig.GetBoolean("AllowScriptCrossing", m_allowScriptCrossings);
                m_dontPersistBefore = startupConfig.GetLong("MinimumTimeBeforePersistenceConsidered", DEFAULT_MIN_TIME_FOR_PERSISTENCE);
                m_dontPersistBefore *= 10000000;
                m_persistAfter = startupConfig.GetLong("MaximumTimeBeforePersistenceConsidered", DEFAULT_MAX_TIME_FOR_PERSISTENCE);
                m_persistAfter *= 10000000;

                m_defaultScriptEngine = startupConfig.GetString("DefaultScriptEngine", "YEngine");
                m_log.InfoFormat("[SCENE]: Default script engine {0}", m_defaultScriptEngine);

                m_strictAccessControl = startupConfig.GetBoolean("StrictAccessControl", m_strictAccessControl);
                m_seeIntoBannedRegion = startupConfig.GetBoolean("SeeIntoBannedRegion", m_seeIntoBannedRegion);

                string[] possibleMapConfigSections = new string[] { "Map", "Startup" };

                m_generateMaptiles
                    = Util.GetConfigVarFromSections<bool>(config, "GenerateMaptiles", possibleMapConfigSections, true);

                if (m_generateMaptiles)
                {
                    int maptileRefresh = Util.GetConfigVarFromSections<int>(config, "MaptileRefresh", possibleMapConfigSections, 0);
                    m_log.InfoFormat("[SCENE]: Region {0}, WORLD MAP refresh time set to {1} seconds", RegionInfo.RegionName, maptileRefresh);
                    if (maptileRefresh != 0)
                    {
                        m_mapGenerationTimer.Interval = maptileRefresh * 1000;
                        m_mapGenerationTimer.Elapsed += RegenerateMaptileAndReregister;
                        m_mapGenerationTimer.AutoReset = false;
                        m_mapGenerationTimer.Start();
                    }
                }
                else
                {
                    string tile = Util.GetConfigVarFromSections<string>(
                            config, "MaptileStaticUUID", possibleMapConfigSections, Util.UUIDZeroString);

                    if (tile != Util.UUIDZeroString && UUID.TryParse(tile, out UUID tileID))
                    {
                        RegionInfo.RegionSettings.TerrainImageID = tileID;
                    }
                    else
                    {
                        RegionInfo.RegionSettings.TerrainImageID = RegionInfo.MaptileStaticUUID;
                        m_log.InfoFormat("[SCENE]: Region {0}, maptile set to {1}", RegionInfo.RegionName, RegionInfo.MaptileStaticUUID.ToString());
                    }
                }

                string[] possibleAccessControlConfigSections = new string[] { "Startup", "AccessControl"};

                string grant = Util.GetConfigVarFromSections<string>(
                    config, "AllowedClients", possibleAccessControlConfigSections, string.Empty);

                if (grant.Length > 0)
                {
                    foreach (string viewer in grant.Split(','))
                    {
                        m_AllowedViewers.Add(viewer.Trim().ToLower());
                    }
                }

                grant = Util.GetConfigVarFromSections<string>(config, "DeniedClients", possibleAccessControlConfigSections, string.Empty);
                // Deal with the mess of someone having used a different word at some point
                if (string.IsNullOrWhiteSpace(grant))
                    grant = Util.GetConfigVarFromSections<string>(config, "BannedClients", possibleAccessControlConfigSections, string.Empty);

                if (grant.Length > 0)
                {
                    foreach (string viewer in grant.Split(','))
                    {
                        m_BannedViewers.Add(viewer.Trim().ToLower());
                    }
                }

                FrameTime                 = startupConfig.GetFloat( "FrameTime", FrameTime);
                FrameTimeWarnPercent      = startupConfig.GetInt( "FrameTimeWarnPercent", FrameTimeWarnPercent);
                FrameTimeCritPercent      = startupConfig.GetInt( "FrameTimeCritPercent", FrameTimeCritPercent);
                Normalized55FPS           = startupConfig.GetBoolean( "Normalized55FPS", Normalized55FPS);

                m_update_backup           = startupConfig.GetInt("UpdateStorageEveryNFrames",         m_update_backup);
                m_update_coarse_locations = startupConfig.GetInt("UpdateCoarseLocationsEveryNFrames", m_update_coarse_locations);
                m_update_entitymovement   = startupConfig.GetInt("UpdateEntityMovementEveryNFrames",  m_update_entitymovement);
                m_update_events           = startupConfig.GetInt("UpdateEventsEveryNFrames",          m_update_events);
                m_update_objects          = startupConfig.GetInt("UpdateObjectsEveryNFrames",         m_update_objects);
                m_update_physics          = startupConfig.GetInt("UpdatePhysicsEveryNFrames",         m_update_physics);
                m_update_presences        = startupConfig.GetInt("UpdateAgentsEveryNFrames",          m_update_presences);
                m_update_terrain          = startupConfig.GetInt("UpdateTerrainEveryNFrames",         m_update_terrain);
                m_update_temp_cleaning    = startupConfig.GetInt("UpdateTempCleaningEveryNSeconds",   m_update_temp_cleaning);

                string[] possibleScriptConfigSections = new string[] { "YEngine", "Xengine", "Scripts" };
                m_LinkSetDataLimit = Util.GetConfigVarFromSections<int>(config, "LinksetDataLimit", possibleScriptConfigSections, m_LinkSetDataLimit);
            }

            #endregion Region Config

            IConfig entityTransferConfig = m_config.Configs["EntityTransfer"];
            if (entityTransferConfig is not null)
            {
                AllowAvatarCrossing = entityTransferConfig.GetBoolean("AllowAvatarCrossing", AllowAvatarCrossing);
                DisableObjectTransfer = entityTransferConfig.GetBoolean("DisableObjectTransfer", false);
            }

            #region Interest Management

            IConfig interestConfig = m_config.Configs["InterestManagement"];
            if (interestConfig is not null)
            {
                string update_prioritization_scheme = interestConfig.GetString("UpdatePrioritizationScheme", "Time").Trim().ToLower();

                try
                {
                    UpdatePrioritizationScheme = (UpdatePrioritizationSchemes)Enum.Parse(typeof(UpdatePrioritizationSchemes), update_prioritization_scheme, true);
                }
                catch (Exception)
                {
                    m_log.Warn("[PRIORITIZER]: UpdatePrioritizationScheme was not recognized, setting to default BestAvatarResponsiveness");
                    UpdatePrioritizationScheme = UpdatePrioritizationSchemes.BestAvatarResponsiveness;
                }

                IsReprioritizationEnabled
                    = interestConfig.GetBoolean("ReprioritizationEnabled", IsReprioritizationEnabled);
                ReprioritizationInterval
                    = interestConfig.GetFloat("ReprioritizationInterval", ReprioritizationInterval);
                ReprioritizationDistance
                    = interestConfig.GetFloat("RootReprioritizationDistance", ReprioritizationDistance);

                if(ReprioritizationDistance < m_minReprioritizationDistance)
                    ReprioritizationDistance = m_minReprioritizationDistance;

                ObjectsCullingByDistance
                    = interestConfig.GetBoolean("ObjectsCullingByDistance", ObjectsCullingByDistance);

            }

            m_log.DebugFormat("[SCENE]: Using the {0} prioritization scheme", UpdatePrioritizationScheme);

            #endregion Interest Management

            StatsReporter = new SimStatsReporter(this);

            StatsReporter.OnSendStatsResult += SendSimStatsPackets;
            StatsReporter.OnStatsIncorrect += m_sceneGraph.RecalculateStats;

            IConfig restartConfig = config.Configs["RestartModule"];
            if (restartConfig is not null)
            {
                string markerPath = restartConfig.GetString("MarkerPath", string.Empty);
                if (!string.IsNullOrEmpty(markerPath))
                {
                    string path = Path.Combine(markerPath, RegionInfo.RegionID.ToString() + ".started");
                    try
                    {
                        string pidstring = Environment.ProcessId.ToString();
                        FileStream fs = File.Create(path);
                        System.Text.ASCIIEncoding enc = new();
                        Byte[] buf = enc.GetBytes(pidstring);
                        fs.Write(buf, 0, buf.Length);
                        fs.Close();
                    }
                    catch (Exception)
                    {
                    }
                }
            }

            StartTimerWatchdog();
        }

        public Scene(RegionInfo regInfo)
            : base(regInfo)
        {
            m_sceneGraph = new SceneGraph(this);

            // If the scene graph has an Unrecoverable error, restart this sim.
            // Currently the only thing that causes it to happen is two kinds of specific
            // Physics based crashes.
            //
            // Out of memory
            // Operating system has killed the plugin
            m_sceneGraph.UnRecoverableError += () =>
            {
                m_log.Error($"[SCENE]: Restarting region {Name} due to unrecoverable physics crash");
                RestartNow();
            };

            PhysicalPrims = true;
            CollidablePrims = true;
            PhysicsEnabled = true;

            AllowAvatarCrossing = true;

            PeriodicBackup = true;
            UseBackup = true;

            IsReprioritizationEnabled = true;
            UpdatePrioritizationScheme = UpdatePrioritizationSchemes.BestAvatarResponsiveness;
            ReprioritizationInterval = 5000;

            ReprioritizationDistance = m_minReprioritizationDistance;

            m_eventManager = new EventManager();

            m_permissions = new ScenePermissions(this);

        }

        #endregion

        #region Startup / Close Methods

        /// <value>
        /// The scene graph for this scene
        /// </value>
        /// TODO: Possibly stop other classes being able to manipulate this directly.
        public SceneGraph SceneGraph
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return m_sceneGraph; }
        }

        /// <summary>
        /// Called by the module loader when all modules are loaded, after each module's
        /// RegionLoaded hook is called. This is the earliest time where RequestModuleInterface
        /// may be used.
        /// </summary>
        public void AllModulesLoaded()
        {
            IDialogModule dm = RequestModuleInterface<IDialogModule>();

            if (dm is not null)
                m_eventManager.OnPermissionError += dm.SendAlertToUser;

            ISimulatorFeaturesModule fm = RequestModuleInterface<ISimulatorFeaturesModule>();
            if (fm is not null)
            {
                float statisticsFPSfactor = 1.0f;
                if(Normalized55FPS)
                    statisticsFPSfactor = 55.0f * FrameTime;

                fm.AddOpenSimExtraFeature("SimulatorFPS", OSD.FromReal(1.0f / FrameTime));
                fm.AddOpenSimExtraFeature("SimulatorFPSFactor", OSD.FromReal(statisticsFPSfactor));
                fm.AddOpenSimExtraFeature("SimulatorFPSWarnPercent", OSD.FromInteger(FrameTimeWarnPercent));
                fm.AddOpenSimExtraFeature("SimulatorFPSCritPercent", OSD.FromInteger(FrameTimeCritPercent));

                fm.AddOpenSimExtraFeature("MinPrimScale", OSD.FromReal(m_minNonphys));
                fm.AddOpenSimExtraFeature("MaxPrimScale", OSD.FromReal(m_maxNonphys));
                fm.AddOpenSimExtraFeature("MinPhysPrimScale", OSD.FromReal(m_minPhys));
                fm.AddOpenSimExtraFeature("MaxPhysPrimScale", OSD.FromReal(m_maxPhys));

                if(SceneGridInfo is not null)
                {
                    if (!fm.OpenSimExtraFeatureContains("GridName"))
                    {
                        if (!string.IsNullOrEmpty(SceneGridInfo.GridName))
                            fm.AddOpenSimExtraFeature("GridName", SceneGridInfo.GridName);
                    }

                    if (!fm.OpenSimExtraFeatureContains("GridNick"))
                    {
                        if (!string.IsNullOrEmpty(SceneGridInfo.GridNick))
                            fm.AddOpenSimExtraFeature("GridNick", SceneGridInfo.GridNick);
                    }

                    if (!fm.OpenSimExtraFeatureContains("GridURL"))
                    {
                        fm.AddOpenSimExtraFeature("GridURL", SceneGridInfo.GridUrl);
                    }

                    if (!fm.OpenSimExtraFeatureContains("GridURLAlias"))
                    {
                        string[] alias = SceneGridInfo.GridUrlAlias;
                        if(alias is not null && alias.Length > 0)
                        {
                            StringBuilder sb = osStringBuilderCache.Acquire();
                            int i = 0;
                            while(i < alias.Length - 1)
                            {
                                sb.Append(alias[i++]);
                                sb.Append(',');
                            }
                            sb.Append(alias[i]);
                            fm.AddOpenSimExtraFeature("GridURLAlias", osStringBuilderCache.GetStringAndRelease(sb));
                        }
                        else
                            fm.AddOpenSimExtraFeature("GridURLAlias", string.Empty);
                    }

                    if (!fm.OpenSimExtraFeatureContains("search-server-url"))
                    {
                        if (!string.IsNullOrEmpty(SceneGridInfo.SearchURL))
                            fm.AddOpenSimExtraFeature("search-server-url", SceneGridInfo.SearchURL);
                    }

                    if (!fm.OpenSimExtraFeatureContains("destination-guide-url"))
                    {
                        if (!string.IsNullOrEmpty(SceneGridInfo.DestinationGuideURL))
                            fm.AddOpenSimExtraFeature("destination-guide-url", SceneGridInfo.DestinationGuideURL);
                    }

                    if (!fm.OpenSimExtraFeatureContains("currency-base-uri"))
                    {
                        if (!string.IsNullOrEmpty(SceneGridInfo.EconomyURL))
                            fm.AddOpenSimExtraFeature("currency-base-uri", SceneGridInfo.EconomyURL);
                    }
                }
            }
        }

        protected virtual void RegisterDefaultSceneEvents()
        {
            //m_eventManager.OnSignificantClientMovement += HandleOnSignificantClientMovement;
        }

        public override string GetSimulatorVersion()
        {
            return m_simulatorVersion;
        }

        /// <summary>
        /// Process the fact that a neighbouring region has come up.
        /// </summary>
        /// <remarks>
        /// We only add it to the neighbor list if it's within 1 region from here.
        /// Agents may have draw distance values that cross two regions though, so
        /// we add it to the notify list regardless of distance. We'll check
        /// the agent's draw distance before notifying them though.
        /// </remarks>
        /// <param name="otherRegion">RegionInfo handle for the new region.</param>
        /// <returns>True after all operations complete, throws exceptions otherwise.</returns>
        public override void OtherRegionUp(GridRegion otherRegion)
        {
            if (RegionInfo.RegionHandle != otherRegion.RegionHandle)
            {
                if (isNeighborRegion(otherRegion))
                {
                    // Let the grid service module know, so this can be cached
                    m_eventManager.TriggerOnRegionUp(otherRegion);
                    if (EntityTransferModule is not null)
                    {
                        try
                        {
                            List<ulong> old = new() { otherRegion.RegionHandle };
                            ForEachRootScenePresence(delegate(ScenePresence agent)
                            {
                                if(agent.IsNPC)
                                    return;

                                agent.DropOldNeighbours(old);
                                EntityTransferModule.EnableChildAgent(agent, otherRegion);
                            });
                        }
                        catch
                        {
                            m_log.Error("[SCENE]: Couldn't inform clients of regionup");
                    
                        }
                    }
                }
                else
                {
                    m_log.Info(
                        $"[SCENE]: Got notice about far away Region: {otherRegion.RegionName} at ({otherRegion.RegionLocX}, {otherRegion.RegionLocY})");
                }
            }
        }

        public bool isNeighborRegion(GridRegion otherRegion)
        {
            int tmp = otherRegion.RegionLocX - (int)RegionInfo.WorldLocX;

            if (tmp < -otherRegion.RegionSizeX || tmp > RegionInfo.RegionSizeX)
                return false;

            tmp = otherRegion.RegionLocY - (int)RegionInfo.WorldLocY;

            if (tmp < -otherRegion.RegionSizeY || tmp > RegionInfo.RegionSizeY)
                return false;

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

        // Alias IncomingHelloNeighbour OtherRegionUp, for now
        public GridRegion IncomingHelloNeighbour(RegionInfo neighbour)
        {
            OtherRegionUp(new GridRegion(neighbour));
            return new GridRegion(RegionInfo);
        }

        // This causes the region to restart immediatley.
        public void RestartNow()
        {
            IConfig startupConfig = m_config.Configs["Startup"];
            if (startupConfig is not null)
            {
                if (startupConfig.GetBoolean("InworldRestartShutsDown", false))
                {
                    MainConsole.Instance.RunCommand("shutdown");
                    return;
                }
            }

            m_log.Info($"[REGION]: Restarting region {Name}");

            Close();

            base.Restart();
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
                if(EntityTransferModule is not null)
                {
                    foreach (RegionInfo region in m_regionRestartNotifyList)
                    {
                        GridRegion r = new(region);
                        try
                        {
                            ForEachRootScenePresence(delegate(ScenePresence agent)
                            {
                                if (!agent.IsNPC)
                                    EntityTransferModule.EnableChildAgent(agent, r);
                            });
                        }
                        catch (NullReferenceException)
                        {
                            // This means that we're not booted up completely yet.
                            // This shouldn't happen too often anymore.
                        }
                    }
                }

                // Reset list to nothing.
                m_regionRestartNotifyList.Clear();
            }
        }

        public int GetInaccurateNeighborCount()
        {
            return m_neighbours.Count;
        }

        // This is the method that shuts down the scene.
        public override void Close()
        {
            if (m_shuttingDown)
            {
                m_log.Warn($"[SCENE]: Ignoring close request because already closing {Name}");
                return;
            }

            IEtcdModule etcd = RequestModuleInterface<IEtcdModule>();
            if (etcd is not null)
            {
                etcd.Delete("Health");
                etcd.Delete("HealthFlags");
                etcd.Delete("RootAgents");
            }

            m_log.Info($"[SCENE]: Closing down the single simulator: {Name}");

            StatsReporter.Close();
            m_restartTimer.Stop();
            m_restartTimer.Close();

            if (!GridService.DeregisterRegion(RegionInfo.RegionID))
                m_log.Warn($"[SCENE]: Deregister from grid failed for region {Name}");

            // Kick all ROOT agents with the message, 'The simulator is going down'
            ForEachScenePresence(delegate(ScenePresence avatar)
            {
                avatar.RemoveNeighbourRegion(RegionInfo.RegionHandle);

                if (!avatar.IsChildAgent)
                    avatar.ControllingClient.Kick("The simulator is going down.");

                avatar.ControllingClient.SendShutdownConnectionNotice();
            });

            // Stop updating the scene objects and agents.
            m_shuttingDown = true;

            // Wait here, or the kick messages won't actually get to the agents before the scene terminates.
            // We also need to wait to avoid a race condition with the scene update loop which might not yet
            // have checked ShuttingDown.
            Thread.Sleep(500);

            // Stop all client threads.
            ForEachScenePresence(delegate(ScenePresence avatar) { CloseAgent(avatar.UUID, false); });

            m_log.Debug("[SCENE]: TriggerSceneShuttingDown");
            EventManager.TriggerSceneShuttingDown(this);

            m_log.Debug("[SCENE]: Persisting changed objects");
            Backup(true);

            m_log.Debug("[SCENE]: Closing scene");

            m_sceneGraph.Close();

            base.Close();

            // XEngine currently listens to the EventManager.OnShutdown event to trigger script stop and persistence.
            // Therefore. we must dispose of the PhysicsScene after this to prevent a window where script code can
            // attempt to reference a null or disposed physics scene.
            if (PhysicsScene is not null)
            {
                m_log.Debug("[SCENE]: Dispose Physics");
                PhysicsScene phys = PhysicsScene;
                // remove the physics engine from both Scene and SceneGraph
                PhysicsScene = null;
                phys.Dispose();
                phys = null;
            }
        }

        public override void Start()
        {
            Start(true);
        }

        /// <summary>
        /// Start the scene
        /// </summary>
        /// <param name='startScripts'>
        /// Start the scripts within the scene.
        /// </param>
        public void Start(bool startScripts)
        {
            if (IsRunning)
                return;

            m_isRunning = true;
            m_active = true;

            m_unixStartTime = Util.UnixTimeSinceEpoch();
//            m_log.DebugFormat("[SCENE]: Starting Heartbeat timer for {0}", RegionInfo.RegionName);
            if (m_heartbeatThread is not null)
            {
                m_hbRestarts++;
                if(m_hbRestarts > 10)
                    Environment.Exit(1);
                m_log.Error($"[SCENE]: Restarting heartbeat thread because it hasn't reported in in region {Name}");
//int pid = System.Diagnostics.Process.GetCurrentProcess().Id;
//System.Diagnostics.Process proc = new System.Diagnostics.Process();
//proc.EnableRaisingEvents=false;
//proc.StartInfo.FileName = "/bin/kill";
//proc.StartInfo.Arguments = "-QUIT " + pid.ToString();
//proc.Start();
//proc.WaitForExit();
//Thread.Sleep(1000);
//Environment.Exit(1);
                //m_heartbeatThread.Abort();
                Watchdog.AbortThread(m_heartbeatThread.ManagedThreadId);
                m_heartbeatThread = null;
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            // tell physics to finish building actor
            m_sceneGraph.ProcessPhysicsPreSimulation();

            m_heartbeatThread = WorkManager.StartThread(
                Heartbeat, $"Heartbeat-({Name.Replace(" ", "_")})", ThreadPriority.Normal, false,
                false, null, 20000, false);
            StartScripts();
        }

        /// <summary>
        /// Sets up references to modules required by the scene
        /// </summary>
        public void SetModuleInterfaces()
        {
            m_xmlrpcModule = RequestModuleInterface<IXMLRPC>();
            m_worldCommModule = RequestModuleInterface<IWorldComm>();
            XferManager = RequestModuleInterface<IXfer>();
            m_AvatarFactory = RequestModuleInterface<IAvatarFactoryModule>();
            AttachmentsModule = RequestModuleInterface<IAttachmentsModule>();
            m_serialiser = RequestModuleInterface<IRegionSerialiserModule>();
            m_dialogModule = RequestModuleInterface<IDialogModule>();
            m_capsModule = RequestModuleInterface<ICapabilitiesModule>();
            EntityTransferModule = RequestModuleInterface<IEntityTransferModule>();
            m_groupsModule = RequestModuleInterface<IGroupsModule>();
            AgentTransactionsModule = RequestModuleInterface<IAgentAssetTransactions>();
            UserManagementModule = RequestModuleInterface<IUserManagement>();
        }

        #endregion

        #region Update Methods

        /// <summary>
        /// Activate the various loops necessary to continually update the scene.
        /// </summary>
        private void Heartbeat()
        {
            m_eventManager.TriggerOnRegionStarted(this);

            // The first frame can take a very long time due to physics actors being added on startup.  Therefore,
            // don't turn on the watchdog alarm for this thread until the second frame, in order to prevent false
            // alarms for scenes with many objects.
            Update(1);

            Watchdog.GetCurrentThreadInfo().AlarmIfTimeout = true;
            m_lastFrameTick = Util.EnvironmentTickCount();
            Update(-1);
            Watchdog.RemoveThread();
        }

        public override void Update(int frames)
        {
            long endFrame;

            if (frames >= 0)
                endFrame = Frame + frames;
            else
                endFrame = long.MaxValue;

            float physicsFPS = 0f;
            float frameTimeMS = FrameTime * 1000.0f;

            int previousFrameTick;

            double lastMS;
            double nowMS;
            double framestart;
            float sleepMS;
            float sleepError = 0;

            while (!m_shuttingDown && ((endFrame == long.MaxValue && Active) || Frame < endFrame))
            {
                framestart = Util.GetTimeStampMS();
                ++Frame;

                // m_log.DebugFormat("[SCENE]: Processing frame {0} in {1}", Frame, RegionInfo.RegionName);

                otherMS = agentMS = tempOnRezMS = eventMS = backupMS = terrainMS = landMS = 0f;

                try
                {
                    EventManager.TriggerRegionHeartbeatStart(this);

                    // Apply taints in terrain module to terrain in physics scene

                    lastMS = Util.GetTimeStampMS();

                    if (Frame % 4 == 0)
                    {
                        CheckTerrainUpdates();
                    }

                    if (Frame % m_update_terrain == 0)
                    {
                        UpdateTerrain();
                    }

                    nowMS = Util.GetTimeStampMS();
                    terrainMS = (float)(nowMS - lastMS);
                    lastMS = nowMS;

                    if (m_physicsEnabled && Frame % m_update_physics == 0)
                        m_sceneGraph.UpdatePreparePhysics();

                    nowMS = Util.GetTimeStampMS();
                    physicsMS2 = (float)(nowMS - lastMS);
                    lastMS = nowMS;

/*
                    // Apply any pending avatar force input to the avatar's velocity
                    if (Frame % m_update_entitymovement == 0)
                        m_sceneGraph.UpdateScenePresenceMovement();
*/
                    if (!m_sendingCoarseLocations && GetNumberOfClients() > 0 && Frame % m_update_coarse_locations == 0)
                    {
                        m_sendingCoarseLocations = true;
                        WorkManager.RunInThreadPool(
                            delegate
                            {
                                SceneGraph.GetCoarseLocations(out List<Vector3> coarseLocations, out List<UUID> avatarUUIDs, 60);
                                // Send coarse locations to clients
                                ForEachScenePresence(delegate(ScenePresence presence)
                                {
                                    if(!presence.IsNPC)
                                        presence.SendCoarseLocations(coarseLocations, avatarUUIDs);
                                });
                                m_sendingCoarseLocations = false; 
                            }, null, string.Format("SendCoarseLocations ({0})", Name));
                    }

                    // Get the simulation frame time that the avatar force input
                    // took
                    nowMS = Util.GetTimeStampMS();
                    agentMS = (float)(nowMS - lastMS);
                    lastMS = nowMS;

                    // Perform the main physics update.  This will do the actual work of moving objects and avatars according to their
                    // velocity
                    if (m_physicsEnabled && Frame % m_update_physics == 0)
                    {
                        physicsFPS = m_sceneGraph.UpdatePhysics(FrameTime);
                    }

                    nowMS = Util.GetTimeStampMS();
                    physicsMS = (float)(nowMS - lastMS);
                    lastMS = nowMS;

                    //CheckKFM(FrameTime);
                    // Check if any objects have reached their targets
                    CheckAtTargets();

                    // Update SceneObjectGroups that have scheduled themselves for updates
                    // Objects queue their updates onto all scene presences
                    if (Frame % m_update_objects == 0)
                        m_sceneGraph.UpdateObjectGroups();

                    nowMS = Util.GetTimeStampMS();
                    otherMS = (float)(nowMS - lastMS);
                    lastMS = nowMS;

                    // Run through all ScenePresences looking for updates
                    // Presence updates and queued object updates for each presence are sent to clients
                    if (Frame % m_update_presences == 0)
                        m_sceneGraph.UpdatePresences();

                    nowMS = Util.GetTimeStampMS();
                    agentMS += (float)(nowMS - lastMS);
                    lastMS = nowMS;

                    // Delete temp-on-rez stuff
                    if (Frame % m_update_temp_cleaning == 0 && !m_cleaningTemps)
                    {
                        m_cleaningTemps = true;
                        WorkManager.RunInThreadPool(
                            delegate { CleanTempObjects(); m_cleaningTemps = false; }, null, $"CleanTempObjects ({Name})");
                        nowMS = Util.GetTimeStampMS();
                        tempOnRezMS = (float)(nowMS - lastMS); // bad.. counts the FireAndForget, not CleanTempObjects
                        lastMS = nowMS;
                    }

                    if (Frame % m_update_events == 0)
                    {
                        UpdateEvents();

                        nowMS = Util.GetTimeStampMS();
                        eventMS = (float)(nowMS - lastMS);
                        lastMS = nowMS;
                    }

                    if (PeriodicBackup && Frame % m_update_backup == 0)
                    {
                        UpdateStorageBackup();

                        nowMS = Util.GetTimeStampMS();
                        backupMS = (float)(nowMS - lastMS);
                        lastMS = nowMS;
                    }

                    //if (Frame % m_update_land == 0)
                    //{
                    //    int ldMS = Util.EnvironmentTickCount();
                    //    UpdateLand();
                    //    landMS = Util.EnvironmentTickCountSubtract(ldMS);
                    //}

                    if (!LoginsEnabled && Frame == 20)
                    {
                        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        GC.Collect();
                        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.Default;

                        if (!LoginLock)
                        {
                            if (!StartDisabled)
                            {
                                m_log.Info($"[REGION]: Enabling logins for {Name}");
                                LoginsEnabled = true;
                            }

                            m_sceneGridService.InformNeighborsThatRegionisUp(
                                RequestModuleInterface<INeighbourService>(), RegionInfo);

                            // Region ready should always be set
                            Ready = true;

                            IConfig restartConfig = m_config.Configs["RestartModule"];
                            if (restartConfig is not null)
                            {
                                string markerPath = restartConfig.GetString("MarkerPath", string.Empty);

                                if (!string.IsNullOrEmpty(markerPath))
                                {
                                    string path = Path.Combine(markerPath, RegionInfo.RegionID.ToString() + ".ready");
                                    try
                                    {
                                        string pidstring = Environment.ProcessId.ToString();
                                        FileStream fs = File.Create(path);
                                        System.Text.ASCIIEncoding enc = new();
                                        Byte[] buf = enc.GetBytes(pidstring);
                                        fs.Write(buf, 0, buf.Length);
                                        fs.Close();
                                    }
                                    catch (Exception)
                                    {
                                    }
                                }
                            }
                        }
                        else
                        {
                            // This handles a case of a region having no scripts for the RegionReady module
                            if (m_sceneGraph.GetActiveScriptsCount() == 0)
                            {
                                // In this case, we leave it to the IRegionReadyModule to enable logins

                                // LoginLock can currently only be set by a region module implementation.
                                // If somehow this hasn't been done then the quickest way to bugfix is to see the
                                // NullReferenceException

                                IRegionReadyModule rrm = RequestModuleInterface<IRegionReadyModule>();
                                rrm.TriggerRegionReady(this);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    m_log.Error($"[SCENE]: Failed on region {Name}: {e.Message}:{e.StackTrace}");
                }

                EventManager.TriggerRegionHeartbeatEnd(this);
                m_firstHeartbeat = false;
                Watchdog.UpdateThread();

                otherMS += tempOnRezMS + eventMS + backupMS + terrainMS + landMS;

                lastMS = Util.GetTimeStampMS();

                previousFrameTick = m_lastFrameTick;
                m_lastFrameTick = (int)(lastMS + 0.5);

                // estimate sleep time
                nowMS = lastMS - framestart;
                nowMS = (double)frameTimeMS - nowMS - sleepError;

                // reuse frameMS as temporary
                frameMS = (float)nowMS;

                // sleep if we can
                if (nowMS > 0)
                    {
                    Thread.Sleep((int)(nowMS + 0.5));

                    nowMS = Util.GetTimeStampMS();
                    sleepMS = (float)(nowMS - lastMS);
                    sleepError = sleepMS - frameMS;
                    sleepError = Math.Clamp(sleepError, 0.0f, 20f);
                    frameMS = (float)(nowMS - framestart);
                    }
                else
                    {
                    nowMS = Util.GetTimeStampMS();
                    frameMS = (float)(nowMS - framestart);
                    sleepMS = 0.0f;
                    sleepError = 0.0f;
                    }

                // script time is not scene frame time, but is displayed per frame
                float scriptTimeMS = GetAndResetScriptExecutionTime();
                StatsReporter.AddFrameStats(TimeDilation, physicsFPS, agentMS,
                             physicsMS + physicsMS2, otherMS , sleepMS, frameMS, scriptTimeMS);

                // Optionally warn if a frame takes double the amount of time that it should.
                if (DebugUpdates
                    && Util.EnvironmentTickCountSubtract(
                        m_lastFrameTick, previousFrameTick) > (int)(FrameTime * 1000 * 2))

                    m_log.WarnFormat(
                        "[SCENE]: Frame took {0} ms (desired max {1} ms) in {2}",
                        Util.EnvironmentTickCountSubtract(m_lastFrameTick, previousFrameTick),
                        FrameTime * 1000,

                        RegionInfo.RegionName);
            }
        }

        /// <summary>
        /// Adds the execution time of one script to the total scripts execution time for this region.
        /// </summary>
        /// <param name="ticks">Elapsed Stopwatch ticks</param>
        public void AddScriptExecutionTime(long ticks)
        {
            StatsReporter.addScriptEvents(1);
            Interlocked.Add(ref m_scriptExecutionTime, ticks);
        }

        public void AddScriptEvents(int cntr)
        {
            StatsReporter.addScriptEvents(cntr);
        }

        /// <summary>
        /// Returns the total execution time of all the scripts in the region since the last call
        /// (in milliseconds), and clears the value in preparation for the next call.
        /// </summary>
        /// <returns>Time in milliseconds</returns>

        // Warning: this is now called from StatsReporter, and can't be shared

        public long GetAndResetScriptExecutionTime()
        {
            long ticks = Interlocked.Exchange(ref m_scriptExecutionTime, 0);
            return (ticks * 1000L) / Stopwatch.Frequency;
        }

        public void AddGroupTarget(SceneObjectGroup grp)
        {
            lock (m_groupsWithTargets)
                m_groupsWithTargets.Add(grp.UUID);
        }

        public void RemoveGroupTarget(SceneObjectGroup grp)
        {
            lock (m_groupsWithTargets)
                m_groupsWithTargets.Remove(grp.UUID);
        }

        private void CheckAtTargets()
        {
            List<UUID> objs = null;

            lock (m_groupsWithTargets)
            {
                if (m_groupsWithTargets.Count != 0)
                    objs = new List<UUID>(m_groupsWithTargets);
            }

            if (objs is not null)
            {
                for(int i = 0; i< objs.Count; ++i)
                {
                    UUID entry = objs[i];
                    SceneObjectGroup grp = GetSceneObjectGroup(entry);
                    if (grp is null)
                        m_groupsWithTargets.Remove(entry);
                    else
                        grp.CheckAtTargets();
                }
            }
        }

        /// <summary>
        /// Send out simstats data to all clients
        /// </summary>
        /// <param name="stats">Stats on the Simulator's performance</param>
        private void SendSimStatsPackets(SimStats stats)
        {
            ForEachRootClient(delegate(IClientAPI client)
            {
                client.SendSimStats(stats);
            });
        }

        /// <summary>
        /// Update the terrain if it needs to be updated.
        /// </summary>
        private void UpdateTerrain()
        {
            EventManager.TriggerTerrainTick();
        }

        private void CheckTerrainUpdates()
        {
            EventManager.TriggerTerrainCheckUpdates();
        }

        /// <summary>
        /// Back up queued up changes
        /// </summary>
        private void UpdateStorageBackup()
        {
            if (!m_backingup)
            {
                WorkManager.RunInThreadPool(o => Backup(false), null, string.Format("BackupWorker ({0})", Name), false);
            }
        }

        /// <summary>
        /// Sends out the OnFrame event to the modules
        /// </summary>
        private void UpdateEvents()
        {
            m_eventManager.TriggerOnFrame();
        }

        /// <summary>
        /// Backup the scene.
        /// </summary>
        /// <remarks>
        /// This acts as the main method of the backup thread.  In a regression test whether the backup thread is not
        /// running independently this can be invoked directly.
        /// </remarks>
        /// <param name="forced">
        /// If true, then any changes that have not yet been persisted are persisted.  If false,
        /// then the persistence decision is left to the backup code (in some situations, such as object persistence,
        /// it's much more efficient to backup multiple changes at once rather than every single one).
        /// <returns></returns>
        public void Backup(bool forced)
        {
            lock (m_returns)
            {
                if(m_backingup)
                {
                    m_log.Warn($"[Scene] Backup of {Name} already running. New call skipped");
                    return;
                }

                m_backingup = true;
                try
                {
                    EventManager.TriggerOnBackup(SimulationDataService, forced);

                    if(m_returns.Count == 0)
                        return;

                    IMessageTransferModule tr = RequestModuleInterface<IMessageTransferModule>();
                    if (tr is null)
                        return;

                    uint unixtime = (uint)Util.UnixTimeSinceEpoch();
                    uint estateid =  RegionInfo.EstateSettings.ParentEstateID;
                    Guid regionguid = RegionInfo.RegionID.Guid;
 
                    foreach (KeyValuePair<UUID, ReturnInfo> ret in m_returns)
                    {
                        GridInstantMessage msg = new()
                        {
                            fromAgentID = Guid.Empty, // From server
                            toAgentID = ret.Key.Guid,
                            imSessionID = Guid.NewGuid(),
                            timestamp = unixtime,
                            fromAgentName = "Server",
                            dialog = 19, // Object msg
                            fromGroup = false,
                            offline = 1,
                            ParentEstateID = estateid,
                            Position = Vector3.Zero,
                            RegionID = regionguid,
                            // We must fill in a null-terminated 'empty' string here since bytes[0] will crash viewer 3.
                            binaryBucket = new Byte[1] {0}
                        };

                        if (ret.Value.count > 1)
                            msg.message = $"Your {ret.Value.count} objects were returned from {ret.Value.location} in region {Name} due to {ret.Value.reason}";
                        else
                            msg.message = $"Your object {ret.Value.objectName} was returned from {ret.Value.location} in region {Name} due to {ret.Value.reason}";

                        tr.SendInstantMessage(msg, delegate(bool success) { });
                    }
                    m_returns.Clear();
                }
                finally
                {
                    m_backingup = false;
                }
            }
        }

        /// <summary>
        /// Synchronous force backup.  For deletes and links/unlinks
        /// </summary>
        /// <param name="group">Object to be backed up</param>
        public void ForceSceneObjectBackup(SceneObjectGroup group)
        {
            if (group is not null)
            {
                group.HasGroupChanged = true;
                group.ProcessBackup(SimulationDataService, true);
            }
        }

        /// <summary>
        /// Tell an agent that their object has been returned.
        /// </summary>
        /// <remarks>
        /// The actual return is handled by the caller.
        /// </remarks>
        /// <param name="agentID">Avatar Unique Id</param>
        /// <param name="objectName">Name of object returned</param>
        /// <param name="location">Location of object returned</param>
        /// <param name="reason">Reasion for object return</param>
        public void AddReturn(UUID agentID, string objectName, Vector3 location, string reason)
        {
            lock (m_returns)
            {
                if (m_returns.TryGetValue(agentID, out ReturnInfo info))
                {
                    info.count++;
                }
                else
                {
                    info = new ReturnInfo()
                    {
                        count = 1,
                        objectName = objectName,
                        location = location,
                        reason = reason
                    };
                }
                m_returns[agentID] = info;
            }
        }

        #endregion

        #region Load Terrain

        /// <summary>
        /// Store the terrain in the persistant data store
        /// </summary>
        public void SaveTerrain()
        {
            SimulationDataService.StoreTerrain(Heightmap.GetTerrainData(), RegionInfo.RegionID);
        }

        /// <summary>
        /// Store the terrain in the persistant data store
        /// </summary>
        public void SaveBakedTerrain()
        {
            if(Bakedmap is not null)
                SimulationDataService.StoreBakedTerrain(Bakedmap.GetTerrainData(), RegionInfo.RegionID);
        }

        private ViewerEnvironment m_regionEnvironment;
        public ViewerEnvironment RegionEnvironment
        {
            get
            {
                return m_regionEnvironment;
            }
            set
            {
                m_regionEnvironment = value;
            }
        }

        /// <summary>
        /// Loads the World heightmap
        /// </summary>
        public override void LoadWorldMap()
        {
            try
            {
                Bakedmap = null;
                TerrainData map = SimulationDataService.LoadBakedTerrain(RegionInfo.RegionID, (int)RegionInfo.RegionSizeX, (int)RegionInfo.RegionSizeY, (int)RegionInfo.RegionSizeZ);
                if (map is not null)
                {
                    Bakedmap = new TerrainChannel(map);
                }
            }
            catch (Exception e)
            {
                m_log.WarnFormat(
                    "[TERRAIN]: Scene.cs: LoadWorldMap() baked terrain - Failed with exception {0}{1}", e.Message, e.StackTrace);
            }

            try
            {
                TerrainData map = SimulationDataService.LoadTerrain(RegionInfo.RegionID, (int)RegionInfo.RegionSizeX, (int)RegionInfo.RegionSizeY, (int)RegionInfo.RegionSizeZ);
                if (map is null)
                {
                    if(Bakedmap is not null)
                    {
                        m_log.Warn("[TERRAIN]: terrain not found. Used stored baked terrain.");
                        Heightmap = Bakedmap.MakeCopy();
                        SimulationDataService.StoreTerrain(Heightmap.GetTerrainData(), RegionInfo.RegionID);
                    }
                    else
                    {
                        // This should be in the Terrain module, but it isn't because
                        // the heightmap is needed _way_ before the modules are initialized...
                        IConfig terrainConfig = m_config.Configs["Terrain"];
                        String m_InitialTerrain = "pinhead-island";
                        if (terrainConfig is not null)
                            m_InitialTerrain = terrainConfig.GetString("InitialTerrain", m_InitialTerrain);

                        m_log.InfoFormat("[TERRAIN]: No default terrain. Generating a new terrain {0}.", m_InitialTerrain);
                        Heightmap = new TerrainChannel(m_InitialTerrain, (int)RegionInfo.RegionSizeX, (int)RegionInfo.RegionSizeY, (int)RegionInfo.RegionSizeZ);

                        SimulationDataService.StoreTerrain(Heightmap.GetTerrainData(), RegionInfo.RegionID);
                    }
                }
                else
                {
                    Heightmap = new TerrainChannel(map);
                }
            }
            catch (IOException e)
            {
                m_log.WarnFormat(
                    "[TERRAIN]: Scene.cs: LoadWorldMap() - Regenerating as failed with exception {0}{1}",
                    e.Message, e.StackTrace);

#pragma warning disable 0162
                if ((int)Constants.RegionSize != 256)
                {
                    Heightmap = new TerrainChannel();

                    SimulationDataService.StoreTerrain(Heightmap.GetTerrainData(), RegionInfo.RegionID);
                }
            }
            catch (Exception e)
            {
                m_log.WarnFormat(
                    "[TERRAIN]: Scene.cs: LoadWorldMap() - Failed with exception {0}{1}", e.Message, e.StackTrace);
            }

            if(Bakedmap is null && Heightmap is not null)
            {
                Bakedmap = Heightmap.MakeCopy();
                SimulationDataService.StoreBakedTerrain(Bakedmap.GetTerrainData(), RegionInfo.RegionID);
            }
        }

        /// <summary>
        /// Register this region with a grid service
        /// </summary>
        /// <exception cref="System.Exception">Thrown if registration of the region itself fails.</exception>
        public void RegisterRegionWithGrid()
        {
            m_sceneGridService.SetScene(this);

            //// Unfortunately this needs to be here and it can't be async.
            //// The map tile image is stored in RegionSettings, but it also needs to be
            //// stored in the GridService, because that's what the world map module uses
            //// to send the map image UUIDs (of other regions) to the viewer...
            if (m_generateMaptiles)
                RegenerateMaptile();

            GridRegion region = new(RegionInfo);
            string error = GridService.RegisterRegion(RegionInfo.ScopeID, region);
            // m_log.DebugFormat("[SCENE]: RegisterRegionWithGrid. name={0},id={1},loc=<{2},{3}>,size=<{4},{5}>",
            //                    m_regionName,
            //                    RegionInfo.RegionID,
            //                    RegionInfo.RegionLocX, RegionInfo.RegionLocY,
            //                    RegionInfo.RegionSizeX, RegionInfo.RegionSizeY);

            if (error != String.Empty)
                throw new Exception(error);
        }

        #endregion

        #region Load Land

        /// <summary>
        /// Loads all Parcel data from the datastore for region identified by regionID
        /// </summary>
        /// <param name="regionID">Unique Identifier of the Region to load parcel data for</param>
        public void loadAllLandObjectsFromStorage(UUID regionID)
        {
            m_log.Info("[SCENE]: Loading land objects from storage");
            List<LandData> landData = SimulationDataService.LoadLandObjects(regionID);

            if (LandChannel is not null)
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
        /// <param name="regionID"></param>
        public virtual void LoadPrimsFromStorage(UUID regionID)
        {
            LoadingPrims = true;
            m_log.Info("[SCENE]: Loading objects from datastore");

            List<SceneObjectGroup> PrimsFromDB = SimulationDataService.LoadObjects(regionID);

            m_log.InfoFormat("[SCENE]: Loaded {0} objects from the datastore", PrimsFromDB.Count);

            foreach (SceneObjectGroup group in PrimsFromDB)
            {
                AddRestoredSceneObject(group, true, true);
                SceneObjectPart rootPart = group.GetPart(group.UUID);
                rootPart.Flags &= ~(PrimFlags.Scripted | PrimFlags.CreateSelected);

                rootPart.TrimPermissions();
                group.InvalidateDeepEffectivePerms();
                EventManager.TriggerOnSceneObjectLoaded(group);
            }

            LoadingPrims = false;
            EventManager.TriggerPrimsLoaded(this);
        }

        public bool SupportsRayCastFiltered()
        {
            if (PhysicsScene is null)
                return false;
            return PhysicsScene.SupportsRaycastWorldFiltered();
        }

        public object RayCastFiltered(Vector3 position, Vector3 direction, float length, int Count, RayFilterFlags filter)
        {
            if (PhysicsScene is null)
                return null;
            return PhysicsScene.RaycastWorld(position, direction, length, Count, filter);
        }

        /// <summary>
        /// Gets a new rez location based on the raycast and the size of the object that is being rezzed.
        /// </summary>
        /// <param name="RayStart"></param>
        /// <param name="RayEnd"></param>
        /// <param name="RayTargetID"></param>
        /// <param name="rot"></param>
        /// <param name="bypassRayCast"></param>
        /// <param name="RayEndIsIntersection"></param>
        /// <param name="frontFacesOnly"></param>
        /// <param name="scale"></param>
        /// <param name="FaceCenter"></param>
        /// <returns></returns>
        public Vector3 GetNewRezLocation(Vector3 RayStart, Vector3 RayEnd, UUID RayTargetID, Quaternion rot, byte bypassRayCast, byte RayEndIsIntersection, bool frontFacesOnly, Vector3 scale, bool FaceCenter)
        {

            Vector3 dir = RayEnd - RayStart;

            float wheight = (float)RegionInfo.RegionSettings.WaterHeight;
            Vector3 wpos = new(0.0f, 0.0f, Constants.MinSimulationHeight);
            // Check for water surface intersection from above
            if ((RayStart.Z > wheight) && (RayEnd.Z < wheight))
            {
                float ratio = (wheight - RayStart.Z) / dir.Z;
                wpos.X = RayStart.X + (ratio * dir.X);
                wpos.Y = RayStart.Y + (ratio * dir.Y);
                wpos.Z = wheight;
            }

            Vector3 pos;

            if (RayEndIsIntersection != (byte)1)
            {
                float dist = dir.Length();
                if (dist != 0)
                {
                    Vector3 direction = dir * (1 / dist);

                    dist += 1.0f;

                    if (SupportsRayCastFiltered())
                    {
                        RayFilterFlags rayfilter = RayFilterFlags.BackFaceCull;
                        rayfilter |= RayFilterFlags.land;
                        rayfilter |= RayFilterFlags.physical;
                        rayfilter |= RayFilterFlags.nonphysical;
                        rayfilter |= RayFilterFlags.LSLPhantom; // ubODE will only see volume detectors

                        // get some more contacts ???
                        int physcount = 4;

                        List<ContactResult> physresults =
                            (List<ContactResult>)RayCastFiltered(RayStart, direction, dist, physcount, rayfilter);
                        if (physresults is not null && physresults.Count > 0)
                        {
                            // look for terrain ?
                            if(RayTargetID.IsZero())
                            {
                                foreach (ContactResult r in physresults)
                                {
                                    if (r.ConsumerID == 0)
                                    {
                                        pos = r.Normal * scale;
                                        pos *= 0.5f;
                                        pos = r.Pos + pos;

                                        if (wpos.Z > pos.Z) pos = wpos;
                                        return pos;
                                    }
                                }
                            }
                            else
                            {
                                foreach (ContactResult r in physresults)
                                {
                                    SceneObjectPart part = GetSceneObjectPart(r.ConsumerID);
                                    if (part is null)
                                        continue;
                                    if (part.UUID == RayTargetID)
                                    {
                                        pos = r.Normal * scale;
                                        pos *= 0.5f;
                                        pos = r.Pos + pos;

                                        if (wpos.Z > pos.Z) pos = wpos;
                                        return pos;
                                    }
                                }
                            }
                            // else the first we got
                            pos = physresults[0].Normal * scale;
                            pos *= 0.5f;
                            pos = physresults[0].Pos + pos;

                            if (wpos.Z > pos.Z)
                                pos = wpos;
                            return pos;
                        }

                    }
                    if (!RayTargetID.IsZero())
                    {
                        SceneObjectPart target = GetSceneObjectPart(RayTargetID);

                        Ray NewRay = new(RayStart, direction);

                        if (target is not null)
                        {
                            // Ray Trace against target here
                            EntityIntersection ei = target.TestIntersectionOBB(NewRay, Quaternion.Identity, frontFacesOnly, FaceCenter);

                            // Un-comment out the following line to Get Raytrace results printed to the console.
                            // m_log.Info("[RAYTRACERESULTS]: Hit:" + ei.HitTF.ToString() + " Point: " + ei.ipoint.ToString() + " Normal: " + ei.normal.ToString());
                            float ScaleOffset = 0.5f;

                            // If we hit something
                            if (ei.HitTF)
                            {
                                Vector3 scaleComponent = ei.AAfaceNormal;
                                if (scaleComponent.X != 0) ScaleOffset = scale.X;
                                if (scaleComponent.Y != 0) ScaleOffset = scale.Y;
                                if (scaleComponent.Z != 0) ScaleOffset = scale.Z;
                                ScaleOffset = Math.Abs(ScaleOffset);
                                Vector3 intersectionpoint = ei.ipoint;
                                Vector3 normal = ei.normal;
                                // Set the position to the intersection point
                                Vector3 offset = (normal * (ScaleOffset / 2f));
                                pos = (intersectionpoint + offset);

                                //Seems to make no sense to do this as this call is used for rezzing from inventory as well, and with inventory items their size is not always 0.5f
                                //And in cases when we weren't rezzing from inventory we were re-adding the 0.25 straight after calling this method
                                // Un-offset the prim (it gets offset later by the consumer method)
                                //pos.Z -= 0.25F;

                                if (wpos.Z > pos.Z) pos = wpos;
                                return pos;
                            }
                        }
                        else
                        {
                            // We don't have a target here, so we're going to raytrace all the objects in the scene.
                            EntityIntersection ei = m_sceneGraph.GetClosestIntersectingPrim(NewRay, true, false);

                            // Un-comment the following line to print the raytrace results to the console.
                            //m_log.Info("[RAYTRACERESULTS]: Hit:" + ei.HitTF.ToString() + " Point: " + ei.ipoint.ToString() + " Normal: " + ei.normal.ToString());

                            if (ei.HitTF)
                            {
                                pos = new Vector3(ei.ipoint.X, ei.ipoint.Y, ei.ipoint.Z);
                            }
                            else
                            {
                                // fall back to our stupid functionality
                                pos = RayEnd;
                            }

                            if (wpos.Z > pos.Z) pos = wpos;
                            return pos;
                        }
                    }
                }
            }

            pos = RayEnd;

            //increase height so its above the ground.
            //should be getting the normal of the ground at the rez point and using that?
            pos.Z += scale.Z / 2f;
            //                return pos;
            // check against posible water intercept
            if (wpos.Z > pos.Z) pos = wpos;
            return pos;
        }


        /// <summary>
        /// Create a New SceneObjectGroup/Part by raycasting
        /// </summary>
        /// <param name="ownerID"></param>
        /// <param name="groupID"></param>
        /// <param name="RayEnd"></param>
        /// <param name="rot"></param>
        /// <param name="shape"></param>
        /// <param name="bypassRaycast"></param>
        /// <param name="RayStart"></param>
        /// <param name="RayTargetID"></param>
        /// <param name="RayEndIsIntersection"></param>
        public virtual void AddNewPrim(UUID ownerID, UUID groupID, Vector3 RayEnd, Quaternion rot, PrimitiveBaseShape shape,
                                       byte bypassRaycast, Vector3 RayStart, UUID RayTargetID,
                                       byte RayEndIsIntersection, uint addFlags)
        {
            Vector3 pos = GetNewRezLocation(RayStart, RayEnd, RayTargetID, rot, bypassRaycast, RayEndIsIntersection, true, new Vector3(0.5f, 0.5f, 0.5f), false);

            if (Permissions.CanRezObject(1, ownerID, pos))
            {
                AddNewPrim(ownerID, groupID, pos, rot, shape, addFlags);
            }
            else
            {
                if (TryGetClient(ownerID, out IClientAPI client))
                    client.SendAlertMessage("You cannot create objects here.");
            }
        }

        public virtual SceneObjectGroup AddNewPrim(UUID ownerID, UUID groupID,
            Vector3 pos, Quaternion rot, PrimitiveBaseShape shape, uint addFlags = 0)
        {
            //m_log.DebugFormat(
            //    "[SCENE]: Scene.AddNewPrim() pcode {0} called for {1} in {2}", shape.PCode, ownerID, RegionInfo.RegionName);

            SceneObjectGroup sceneObject;

            // If an entity creator has been registered for this prim type then use that
            if (m_entityCreators.ContainsKey((PCode)shape.PCode))
            {
                sceneObject = m_entityCreators[(PCode)shape.PCode].CreateEntity(ownerID, groupID, pos, rot, shape);
            }
            else
            {
                // Otherwise, use this default creation code;
                sceneObject = new SceneObjectGroup(ownerID, pos, rot, shape);
                sceneObject.SetGroup(groupID, null);

                if ((addFlags & (uint)PrimFlags.CreateSelected) != 0)
                {
                    sceneObject.IsSelected = true;
                    sceneObject.RootPart.CreateSelected = true;
                }

                if (AgentPreferencesService is not null) // This will override the brave new full perm world!
                {
                    AgentPrefs prefs = AgentPreferencesService.GetAgentPreferences(ownerID);
                    // Only apply user selected prefs if the user set them
                    if (prefs is not null && prefs.PermNextOwner != 0)
                    {
                        sceneObject.RootPart.GroupMask = (uint)prefs.PermGroup;
                        sceneObject.RootPart.EveryoneMask = (uint)prefs.PermEveryone;
                        sceneObject.RootPart.NextOwnerMask = (uint)prefs.PermNextOwner;
                    }
                }

                AddNewSceneObject(sceneObject, true, false);
            }

            if (UserManagementModule is not null)
                sceneObject.RootPart.CreatorIdentification = UserManagementModule.GetUserUUI(ownerID);


            sceneObject.InvalidateDeepEffectivePerms();
            sceneObject.ScheduleGroupForUpdate(PrimUpdateFlags.FullUpdatewithAnimMatOvr);
            return sceneObject;
        }

        /// <summary>
        /// Add an object into the scene that has come from storage
        /// </summary>
        ///
        /// <param name="sceneObject"></param>
        /// <param name="attachToBackup">
        /// If true, changes to the object will be reflected in its persisted data
        /// If false, the persisted data will not be changed even if the object in the scene is changed
        /// </param>
        /// <param name="alreadyPersisted">
        /// If true, we won't persist this object until it changes
        /// If false, we'll persist this object immediately
        /// </param>
        /// <param name="sendClientUpdates">
        /// If true, we send updates to the client to tell it about this object
        /// If false, we leave it up to the caller to do this
        /// </param>
        /// <returns>
        /// true if the object was added, false if an object with the same uuid was already in the scene
        /// </returns>
        public bool AddRestoredSceneObject(
            SceneObjectGroup sceneObject, bool attachToBackup, bool alreadyPersisted, bool sendClientUpdates)
        {
            if (m_sceneGraph.AddRestoredSceneObject(sceneObject, attachToBackup, alreadyPersisted, sendClientUpdates))
            {
                sceneObject.IsDeleted = false;
                EventManager.TriggerObjectAddedToScene(sceneObject);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Add an object into the scene that has come from storage
        /// </summary>
        ///
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
            return AddRestoredSceneObject(sceneObject, attachToBackup, alreadyPersisted, true);
        }

        /// <summary>
        /// Add a newly created object to the scene.  Updates are also sent to viewers.
        /// </summary>
        /// <param name="sceneObject"></param>
        /// <param name="attachToBackup">
        /// If true, the object is made persistent into the scene.
        /// If false, the object will not persist over server restarts
        /// </param>
        /// <returns>true if the object was added.  false if not</returns>
        public bool AddNewSceneObject(SceneObjectGroup sceneObject, bool attachToBackup)
        {
            return AddNewSceneObject(sceneObject, attachToBackup, true);
        }

        /// <summary>
        /// Add a newly created object to the scene
        /// </summary>
        /// <param name="sceneObject"></param>
        /// <param name="attachToBackup">
        /// If true, the object is made persistent into the scene.
        /// If false, the object will not persist over server restarts
        /// </param>
        /// <param name="sendClientUpdates">
        /// If true, updates for the new scene object are sent to all viewers in range.
        /// If false, it is left to the caller to schedule the update
        /// </param>
        /// <returns>true if the object was added.  false if not</returns>
        public bool AddNewSceneObject(SceneObjectGroup sceneObject, bool attachToBackup, bool sendClientUpdates)
        {
            if (m_sceneGraph.AddNewSceneObject(sceneObject, attachToBackup, sendClientUpdates))
            {
                EventManager.TriggerObjectAddedToScene(sceneObject);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Add a newly created object to the scene.
        /// </summary>
        /// <remarks>
        /// This method does not send updates to the client - callers need to handle this themselves.
        /// </remarks>
        /// <param name="sceneObject"></param>
        /// <param name="attachToBackup"></param>
        /// <param name="pos">Position of the object.  If null then the position stored in the object is used.</param>
        /// <param name="rot">Rotation of the object.  If null then the rotation stored in the object is used.</param>
        /// <param name="vel">Velocity of the object.  This parameter only has an effect if the object is physical</param>
        /// <returns></returns>
        public bool AddNewSceneObject(
            SceneObjectGroup sceneObject, bool attachToBackup, Vector3? pos, Quaternion? rot, Vector3 vel)
        {
            if (m_sceneGraph.AddNewSceneObject(sceneObject, attachToBackup, pos, rot, vel))
            {
                EventManager.TriggerObjectAddedToScene(sceneObject);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Delete every object from the scene.  This does not include attachments worn by avatars.
        /// </summary>
        public void DeleteAllSceneObjects()
        {
            DeleteAllSceneObjects(false);
        }

        /// <summary>
        /// Delete every object from the scene.  This does not include attachments worn by avatars.
        /// </summary>
        public void DeleteAllSceneObjects(bool exceptNoCopy)
        {
            List<SceneObjectGroup> toReturn = new();
            lock (Entities)
            {
                EntityBase[] entities = Entities.GetEntities();
                foreach (EntityBase e in entities)
                {
                    if (e is SceneObjectGroup sog)
                    {
                        if (!sog.IsAttachment)
                        {
                            if (!exceptNoCopy || ((sog.EffectiveOwnerPerms & (uint)PermissionMask.Copy) != 0))
                            {
                                DeleteSceneObject(sog, false);
                            }
                            else
                            {
                                toReturn.Add(sog);
                            }
                        }
                    }
                }
            }
            if (toReturn.Count > 0)
            {
                returnObjects(toReturn.ToArray(), null);
            }
        }

        /// <summary>
        /// Synchronously delete the given object from the scene.
        /// </summary>
        /// <remarks>
        /// Scripts are also removed.
        /// </remarks>
        /// <param name="group">Object Id</param>
        /// <param name="silent">Suppress broadcasting changes to other clients.</param>
        public void DeleteSceneObject(SceneObjectGroup group, bool silent)
        {
            DeleteSceneObject(group, silent, true);
        }

        /// <summary>
        /// Synchronously delete the given object from the scene.
        /// </summary>
        /// <param name="group">Object Id</param>
        /// <param name="silent">Suppress broadcasting changes to other clients.</param>
        /// <param name="removeScripts">If true, then scripts are removed.  If false, then they are only stopped.</para>
        public void DeleteSceneObject(SceneObjectGroup group, bool silent, bool removeScripts)
        {
            // m_log.DebugFormat("[SCENE]: Deleting scene object {0} {1}", group.Name, group.UUID);

            if (removeScripts)
                group.RemoveScriptInstances(true);
            else
                group.StopScriptInstances();

            SceneObjectPart[] partList = group.Parts;

            for(int i = 0; i < partList.Length; ++i)
            {
                SceneObjectPart part = partList[i];
                if (part is null)
                    continue;

                if (removeScripts)
                    part.Inventory?.SendReleaseScriptsControl();

                if (part.KeyframeMotion is not null)
                {
                    part.KeyframeMotion.Delete();
                    part.KeyframeMotion = null;
                }

                if ((part.AggregatedScriptEvents & scriptEvents.email) != 0)
                {
                    IEmailModule imm = RequestModuleInterface<IEmailModule>();
                    imm?.RemovePartMailBox(part.UUID);
                }
                if (part.PhysActor is not null)
                {
                    part.RemoveFromPhysics();
                }
            }

            if (UnlinkSceneObject(group, false))
            {
                EventManager.TriggerObjectBeingRemovedFromScene(group);
                EventManager.TriggerParcelPrimCountTainted();
            }

            group.DeleteGroupFromScene(silent);

            // use this to mean also full delete
            if (removeScripts)
                group.Dispose();
            // m_log.DebugFormat("[SCENE]: Exit DeleteSceneObject() for {0} {1}", group.Name, group.UUID);
        }

        /// <summary>
        /// Unlink the given object from the scene.  Unlike delete, this just removes the record of the object - the
        /// object itself is not destroyed.
        /// </summary>
        /// <param name="so">The scene object.</param>
        /// <param name="softDelete">If true, only deletes from scene, but keeps the object in the database.</param>
        /// <returns>true if the object was in the scene, false if it was not</returns>
        public bool UnlinkSceneObject(SceneObjectGroup so, bool softDelete)
        {
            if (m_sceneGraph.DeleteSceneObject(so.UUID, softDelete))
            {
                if (!softDelete)
                {
                    // If the group contains prims whose SceneGroupID is incorrect then force a
                    // database update, because RemoveObject() works by searching on the SceneGroupID.
                    // This is an expensive thing to do so only do it if absolutely necessary.
                    if (so.GroupContainsForeignPrims)
                        ForceSceneObjectBackup(so);

                    so.DetachFromBackup();
                    SimulationDataService.RemoveObject(so.UUID, RegionInfo.RegionID);
                }

                // We need to keep track of this state in case this group is still queued for further backup.
                so.IsDeleted = true;

                return true;
            }

            return false;
        }


        /* not in use, outdate by async method
                /// <summary>
                /// Move the given scene object into a new region depending on which region its absolute position has moved
                /// into.
                ///
                /// </summary>
                /// <param name="attemptedPosition">the attempted out of region position of the scene object</param>
                /// <param name="grp">the scene object that we're crossing</param>
                public void CrossPrimGroupIntoNewRegion(Vector3 attemptedPosition, SceneObjectGroup grp, bool silent)
                {
                    if (grp is null)
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
                            m_log.Warn("[SCENE]: exception when trying to remove the prim that crossed the border.");
                        }
                        return;
                    }

                    if (grp.RootPart.RETURN_AT_EDGE)
                    {
                        // We remove the object here
                        try
                        {
                            List<SceneObjectGroup> objects = new List<SceneObjectGroup>();
                            objects.Add(grp);
                            SceneObjectGroup[] objectsArray = objects.ToArray();
                            returnObjects(objectsArray, UUID.Zero);
                        }
                        catch (Exception)
                        {
                            m_log.Warn("[SCENE]: exception when trying to return the prim that crossed the border.");
                        }
                        return;
                    }

                    if (EntityTransferModule is not null)
                        EntityTransferModule.Cross(grp, attemptedPosition, silent);
                }
        */

        // Simple test to see if a position is in the current region.
        // This test is mostly used to see if a region crossing is necessary.
        // Assuming the position is relative to the region so anything outside its bounds.
        // Return 'true' if position inside region.

        public bool PositionIsInCurrentRegion(Vector3 pos)
        {
            float t = pos.X;
            if (t < 0 || t >= RegionInfo.RegionSizeX)
                return false;

            t = pos.Y;
            if (t < 0 || t >= RegionInfo.RegionSizeY)
                return false;

            return true;
        }

        /// <summary>
        /// Called when objects or attachments cross the border, or teleport, between regions.
        /// </summary>
        /// <param name="sog"></param>
        /// <returns></returns>
        public bool IncomingCreateObject(Vector3 newPosition, ISceneObject sog)
        {
            //m_log.DebugFormat(" >>> IncomingCreateObject(sog) <<< {0} deleted? {1} isAttach? {2}", ((SceneObjectGroup)sog).AbsolutePosition,
            //    ((SceneObjectGroup)sog).IsDeleted, ((SceneObjectGroup)sog).RootPart.IsAttachment);

            SceneObjectGroup newObject;
            try
            {
                newObject = (SceneObjectGroup)sog;
            }
            catch (Exception e)
            {
                m_log.WarnFormat("[INTERREGION]: Problem casting object, exception {0}{1}", e.Message, e.StackTrace);
                return false;
            }

            if (!EntityTransferModule.HandleIncomingSceneObject(newObject, newPosition))
                return false;

            // Do this as late as possible so that listeners have full access to the incoming object
            EventManager.TriggerOnIncomingSceneObject(newObject);

            return true;
        }

        public bool IncomingAttechments(ScenePresence sp, List<SceneObjectGroup> attachments)
        {
            //m_log.DebugFormat(" >>> IncomingCreateObject(sog) <<< {0} deleted? {1} isAttach? {2}", ((SceneObjectGroup)sog).AbsolutePosition,
            //    ((SceneObjectGroup)sog).IsDeleted, ((SceneObjectGroup)sog).RootPart.IsAttachment);

            if (!EntityTransferModule.HandleIncomingAttachments(sp, attachments) || sp.IsDeleted)
                return false;

            // Do this as late as possible so that listeners have full access to the incoming object
            foreach(SceneObjectGroup newObject in attachments)
                EventManager.TriggerOnIncomingSceneObject(newObject);

            return true;
        }

        /// <summary>
        /// Adds a Scene Object group to the Scene.
        /// Verifies that the creator of the object is not banned from the simulator.
        /// Checks if the item is an Attachment
        /// </summary>
        /// <param name="sceneObject"></param>
        /// <returns>True if the SceneObjectGroup was added, False if it was not</returns>
        public bool AddSceneObject(SceneObjectGroup sceneObject)
        {
            if (sceneObject.OwnerID.IsZero())
            {
                m_log.ErrorFormat("[SCENE]: Owner ID for {0} was zero", sceneObject.UUID);
                return false;
            }

            // If the user is banned, we won't let any of their objects
            // enter. Period.
            //
            int flags = GetUserFlags(sceneObject.OwnerID);
            if (RegionInfo.EstateSettings.IsBanned(sceneObject.OwnerID, flags))
            {
                m_log.InfoFormat("[INTERREGION]: Denied prim crossing for banned avatar {0}", sceneObject.OwnerID);

                return false;
            }

            // Force allocation of new LocalId
            //
            SceneObjectPart[] parts = sceneObject.Parts;
            for (int i = 0; i < parts.Length; i++)
                parts[i].LocalId = 0;

            if (sceneObject.IsAttachmentCheckFull()) // Attachment
            {
                sceneObject.RootPart.AddFlag(PrimFlags.TemporaryOnRez);

                // Don't sent a full update here because this will cause full updates to be sent twice for
                // attachments on region crossings, resulting in viewer glitches.
                AddRestoredSceneObject(sceneObject, false, false, false);

                // Handle attachment special case
                SceneObjectPart RootPrim = sceneObject.RootPart;

                // Fix up attachment Parent Local ID
                ScenePresence sp = GetScenePresence(sceneObject.OwnerID);

                if (sp is not null)
                {
                    SceneObjectGroup grp = sceneObject;

                    // m_log.DebugFormat(
                    //     "[ATTACHMENT]: Received attachment {0}, inworld asset id {1}", grp.FromItemID, grp.UUID);
                    // m_log.DebugFormat(
                    //     "[ATTACHMENT]: Attach to avatar {0} at position {1}", sp.UUID, grp.AbsolutePosition);

                    // We must currently not resume scripts at this stage since AttachmentsModule does not have the
                    // information that this is due to a teleport/border cross rather than an ordinary attachment.
                    // We currently do this in Scene.MakeRootAgent() instead.
                    bool attached = false;
                    if (AttachmentsModule is not null)
                        attached = AttachmentsModule.AttachObject(sp, grp, 0, false, false, true);

                    if (attached)
                        RootPrim.RemFlag(PrimFlags.TemporaryOnRez);
                    else
                        m_log.DebugFormat("[SCENE]: Attachment {0} arrived but failed to attach, setting to temp", sceneObject.UUID);
                }
                else
                {
                    m_log.DebugFormat("[SCENE]: Attachment {0} arrived and scene presence was not found, setting to temp", sceneObject.UUID);
//                    RootPrim.RemFlag(PrimFlags.TemporaryOnRez);
//                    RootPrim.AddFlag(PrimFlags.TemporaryOnRez);
                }
                if (sceneObject.OwnerID.IsZero())
                {
                    m_log.ErrorFormat("[SCENE]: Owner ID for {0} was zero after attachment processing. BUG!", sceneObject.UUID);
                    return false;
                }
            }
            else
            {
                if (sceneObject.OwnerID.IsZero())
                {
                    m_log.ErrorFormat("[SCENE]: Owner ID for non-attachment {0} was zero", sceneObject.UUID);
                    return false;
                }
                AddRestoredSceneObject(sceneObject, true, false);
            }

            return true;
        }

        private int GetStateSource(SceneObjectGroup sog)
        {
            if(!sog.IsAttachmentCheckFull())
                return 2; // StateSource.PrimCrossing

            ScenePresence sp = GetScenePresence(sog.OwnerID);
            if (sp is not null)
                return sp.GetStateSource();

            return 2; // StateSource.PrimCrossing
        }

        public int GetUserFlags(UUID user)
        {
            //Unfortunately the SP approach means that the value is cached until region is restarted
            /*
            ScenePresence sp;
            if (TryGetScenePresence(user, out sp))
            {
                return sp.UserFlags;
            }
            else
            {
             */
                UserAccount uac = UserAccountService.GetUserAccount(RegionInfo.ScopeID, user);
                if (uac is null)
                    return 0;
                return uac.UserFlags;
            //}
        }

        #endregion

        #region Add/Remove Avatar Methods

        public override ISceneAgent AddNewAgent(IClientAPI client, PresenceType type)
        {
            ScenePresence sp;
            bool vialogin;
            bool reallyNew = true;

            // Update the number of users attempting to login
            StatsReporter.UpdateUsersLoggingIn(true);

            // Validation occurs in LLUDPServer
            //
            // XXX: A race condition exists here where two simultaneous calls to AddNewAgent can interfere with
            // each other.  In practice, this does not currently occur in the code.
            AgentCircuitData aCircuit = m_authenticateHandler.GetAgentCircuitData(client.CircuitCode);

            // We lock here on AgentCircuitData to prevent a race condition between the thread adding a new connection
            // and a simultaneous one that removes it (as can happen if the client is closed at a particular point
            // whilst connecting).
            //
            // It would be easier to lock across all NewUserConnection(), AddNewAgent() and
            // RemoveClient() calls for all agents, but this would allow a slow call (e.g. because of slow service
            // response in some module listening to AddNewAgent()) from holding up unrelated agent calls.
            //
            // In practice, the lock (this) in LLUDPServer.AddNewClient() currently lock across all
            // AddNewClient() operations (though not other ops).
            // In the future this can be relieved once locking per agent (not necessarily on AgentCircuitData) is improved.
            lock (aCircuit)
            {
                vialogin = (aCircuit.teleportFlags & (uint)(Constants.TeleportFlags.ViaHGLogin | Constants.TeleportFlags.ViaLogin)) != 0;

                CheckHeartbeat();

                sp = GetScenePresence(client.AgentId);

                if (sp is null)
                {
                    m_log.DebugFormat(
                        "[SCENE]: Adding new child scene presence {0} {1} to scene {2} at pos {3}, tpflags: {4}",
                        client.Name, client.AgentId, RegionInfo.RegionName, client.StartPos,
                        ((TPFlags)aCircuit.teleportFlags).ToString());

                    m_clientManager.Add(client);
                    SubscribeToClientEvents(client);

                    sp = m_sceneGraph.CreateAndAddChildScenePresence(client, aCircuit.Appearance, type);
                    aCircuit.Appearance = null;

                    sp.TeleportFlags = (TPFlags)aCircuit.teleportFlags;

                    m_eventManager.TriggerOnNewPresence(sp);
                }
                else
                {
                    // We must set this here so that TriggerOnNewClient and TriggerOnClientLogin can determine whether the
                    // client is for a root or child agent.
                    // XXX: This may be better set for a new client before that client is added to the client manager.
                    // But need to know what happens in the case where a ScenePresence is already present (and if this
                    // actually occurs).


                    m_log.WarnFormat(
                        "[SCENE]: Already found {0} scene presence for {1} in {2} when asked to add new scene presence",
                        sp.IsChildAgent ? "child" : "root", sp.Name, RegionInfo.RegionName);

                    reallyNew = false;
                }
                client.SceneAgent = sp;

                // This is currently also being done earlier in NewUserConnection for real users to see if this
                // resolves problems where HG agents are occasionally seen by others as "Unknown user" in chat and other
                // places.  However, we still need to do it here for NPCs.
                CacheUserName(sp, aCircuit);

                if (reallyNew)
                    EventManager.TriggerOnNewClient(client);

                if (vialogin)
                    EventManager.TriggerOnClientLogin(client);
            }

            // User has logged into the scene so update the list of users logging
            // in
            StatsReporter.UpdateUsersLoggingIn(false);

            m_LastLogin = Util.EnvironmentTickCount();

            return sp;
        }

        /// <summary>
        /// Returns the Home URI of the agent, or null if unknown.
        /// </summary>
        public string GetAgentHomeURI(UUID agentID)
        {
            AgentCircuitData circuit = AuthenticateHandler.GetAgentCircuitData(agentID);
            if (circuit is not null && circuit.ServiceURLs is not null && circuit.ServiceURLs.ContainsKey("HomeURI"))
                return circuit.ServiceURLs["HomeURI"].ToString();
            else
                return null;
        }

        /// <summary>
        /// Cache the user name for later use.
        /// </summary>
        /// <param name="sp"></param>
        /// <param name="aCircuit"></param>
        private void CacheUserName(ScenePresence sp, AgentCircuitData aCircuit)
        {
            if (UserManagementModule is not null)
            {
                string first = aCircuit.firstname;
                string last = aCircuit.lastname;

                if (sp is not null && sp.PresenceType == PresenceType.Npc)
                {
                    UserManagementModule.AddNPCUser(aCircuit.AgentID, first, last);
                }
                else
                {
                    string homeURL = string.Empty;

                    if (aCircuit.ServiceURLs.ContainsKey("HomeURI"))
                        homeURL = aCircuit.ServiceURLs["HomeURI"].ToString();

                    if (aCircuit.lastname.StartsWith("@"))
                    {
                        string[] parts = aCircuit.firstname.Split('.');
                        if (parts.Length >= 2)
                        {
                            first = parts[0];
                            last = parts[1];
                        }
                    }

                    UserManagementModule.AddUser(aCircuit.AgentID, first, last, homeURL);
                }
            }
        }

        private bool VerifyClient(AgentCircuitData aCircuit, System.Net.IPEndPoint ep, out bool vialogin)
        {
            vialogin = false;

            // Do the verification here
            if ((aCircuit.teleportFlags & (uint)Constants.TeleportFlags.ViaHGLogin) != 0)
            {
                m_log.DebugFormat("[SCENE]: Incoming client {0} {1} in region {2} via HG login", aCircuit.firstname, aCircuit.lastname, RegionInfo.RegionName);
                vialogin = true;
                IUserAgentVerificationModule userVerification = RequestModuleInterface<IUserAgentVerificationModule>();
                if (userVerification is not null && ep is not null)
                {
                    if (!userVerification.VerifyClient(aCircuit, ep.Address.ToString()))
                    {
                        // uh-oh, this is fishy
                        m_log.DebugFormat("[SCENE]: User Client Verification for {0} {1} in {2} returned false", aCircuit.firstname, aCircuit.lastname, RegionInfo.RegionName);
                        return false;
                    }
                    else
                        m_log.DebugFormat("[SCENE]: User Client Verification for {0} {1} in {2} returned true", aCircuit.firstname, aCircuit.lastname, RegionInfo.RegionName);

                }
            }

            else if ((aCircuit.teleportFlags & (uint)Constants.TeleportFlags.ViaLogin) != 0)
            {
                m_log.DebugFormat("[SCENE]: Incoming client {0} {1} in region {2} via regular login. Client IP verification not performed.",
                    aCircuit.firstname, aCircuit.lastname, RegionInfo.RegionName);
                vialogin = true;
            }

            return true;
        }

        // Called by Caps, on the first HTTP contact from the client
        public override bool CheckClient(UUID agentID, System.Net.IPEndPoint ep)
        {
            AgentCircuitData aCircuit = m_authenticateHandler.GetAgentCircuitData(agentID);
            if (aCircuit is null)
                return false;

            if (VerifyClient(aCircuit, ep, out bool _))
                return true;

            // if it doesn't pass, we remove the agentcircuitdata altogether
            // and the scene presence and the client, if they exist
            try
            {
                ScenePresence sp = WaitGetScenePresence(agentID);
                if (sp is not null)
                {
                    PresenceService.LogoutAgent(sp.ControllingClient.SessionId);
                    CloseAgent(sp.UUID, false);
                }
                // BANG! SLASH!
                m_authenticateHandler.RemoveCircuit(agentID);
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[SCENE]: Exception while closing aborted client: {0}", e.StackTrace);
            }

            return false;
        }

        /// <summary>
        /// Register for events from the client
        /// </summary>
        /// <param name="client">The IClientAPI of the connected client</param>
        public virtual void SubscribeToClientEvents(IClientAPI client)
        {
            SubscribeToClientTerrainEvents(client);
            SubscribeToClientPrimEvents(client);
            SubscribeToClientPrimRezEvents(client);
            SubscribeToClientInventoryEvents(client);
            SubscribeToClientTeleportEvents(client);
            SubscribeToClientScriptEvents(client);
            SubscribeToClientParcelEvents(client);
            SubscribeToClientGridEvents(client);
            SubscribeToClientNetworkEvents(client);
        }

        public virtual void SubscribeToClientTerrainEvents(IClientAPI client)
        {
//            client.OnRegionHandShakeReply += SendLayerData;
        }

        public virtual void SubscribeToClientPrimEvents(IClientAPI client)
        {
            client.OnUpdatePrimGroupPosition += m_sceneGraph.UpdatePrimGroupPosition;
            client.OnUpdatePrimSinglePosition += m_sceneGraph.UpdatePrimSinglePosition;

            client.onClientChangeObject += m_sceneGraph.ClientChangeObject;

            client.OnUpdatePrimGroupRotation += m_sceneGraph.UpdatePrimGroupRotation;
            client.OnUpdatePrimGroupMouseRotation += m_sceneGraph.UpdatePrimGroupRotation;
            client.OnUpdatePrimSingleRotation += m_sceneGraph.UpdatePrimSingleRotation;
            client.OnUpdatePrimSingleRotationPosition += m_sceneGraph.UpdatePrimSingleRotationPosition;

            client.OnUpdatePrimScale += m_sceneGraph.UpdatePrimScale;
            client.OnUpdatePrimGroupScale += m_sceneGraph.UpdatePrimGroupScale;
            client.OnUpdateExtraParams += m_sceneGraph.UpdateExtraParam;
            client.OnUpdatePrimShape += m_sceneGraph.UpdatePrimShape;
            client.OnUpdatePrimTexture += m_sceneGraph.UpdatePrimTexture;
            client.OnObjectRequest += RequestPrim;
            client.OnObjectSelect += SelectPrim;
            client.OnObjectDeselect += DeselectPrim;
            client.OnDeRezObject += DeRezObjects;

            client.OnObjectName += m_sceneGraph.PrimName;
            client.OnObjectClickAction += m_sceneGraph.PrimClickAction;
            client.OnObjectMaterial += m_sceneGraph.PrimMaterial;
            client.OnLinkObjects += LinkObjects;
            client.OnDelinkObjects += DelinkObjects;
            client.OnObjectDuplicate += DuplicateObject;
            client.OnObjectDuplicateOnRay += doObjectDuplicateOnRay;
            client.OnUpdatePrimFlags += m_sceneGraph.UpdatePrimFlags;
            client.OnRequestObjectPropertiesFamily += m_sceneGraph.RequestObjectPropertiesFamily;
            client.OnObjectPermissions += HandleObjectPermissionsUpdate;
            client.OnGrabObject += ProcessObjectGrab;
            client.OnGrabUpdate += ProcessObjectGrabUpdate;
            client.OnDeGrabObject += ProcessObjectDeGrab;
            client.OnSpinStart += ProcessSpinStart;
            client.OnSpinUpdate += ProcessSpinObject;
            client.OnSpinStop += ProcessSpinObjectStop;
            client.OnUndo += m_sceneGraph.HandleUndo;
            client.OnRedo += m_sceneGraph.HandleRedo;
            client.OnObjectDescription += m_sceneGraph.PrimDescription;
            client.OnObjectIncludeInSearch += m_sceneGraph.MakeObjectSearchable;
            client.OnObjectOwner += ObjectOwner;
            client.OnObjectGroupRequest += HandleObjectGroupUpdate;
        }

        public virtual void SubscribeToClientPrimRezEvents(IClientAPI client)
        {
            client.OnAddPrim += AddNewPrim;
            client.OnRezObject += RezObject;
        }

        public virtual void SubscribeToClientInventoryEvents(IClientAPI client)
        {
            client.OnLinkInventoryItem += HandleLinkInventoryItem;
            client.OnCreateNewInventoryFolder += HandleCreateInventoryFolder;
            client.OnUpdateInventoryFolder += HandleUpdateInventoryFolder;
            client.OnMoveInventoryFolder += HandleMoveInventoryFolder; // 2; //!!
            client.OnFetchInventoryDescendents += HandleFetchInventoryDescendents;
            client.OnPurgeInventoryDescendents += HandlePurgeInventoryDescendents; // 2; //!!
            client.OnFetchInventory += m_asyncInventorySender.HandleFetchInventory;
            client.OnUpdateInventoryItem += UpdateInventoryItem;
            client.OnCopyInventoryItem += CopyInventoryItem;
            client.OnMoveItemsAndLeaveCopy += MoveInventoryItemsLeaveCopy;
            client.OnMoveInventoryItem += MoveInventoryItem;
            client.OnRemoveInventoryItem += RemoveInventoryItem;
            client.OnRemoveInventoryFolder += RemoveInventoryFolder;
            client.OnRezScript += RezScript;
            client.OnRequestTaskInventory += RequestTaskInventory;
            client.OnRemoveTaskItem += RemoveTaskInventory;
            client.OnUpdateTaskInventory += UpdateTaskInventory;
            client.OnMoveTaskItem += ClientMoveTaskInventoryItem;
        }

        public virtual void SubscribeToClientTeleportEvents(IClientAPI client)
        {
            client.OnTeleportLocationRequest += RequestTeleportLocation;
        }

        public virtual void SubscribeToClientScriptEvents(IClientAPI client)
        {
            client.OnScriptReset += ProcessScriptReset;
            client.OnGetScriptRunning += GetScriptRunning;
            client.OnSetScriptRunning += SetScriptRunning;
        }

        public virtual void SubscribeToClientParcelEvents(IClientAPI client)
        {
            client.OnParcelReturnObjectsRequest += LandChannel.ReturnObjectsInParcel;
            client.OnParcelSetOtherCleanTime += LandChannel.SetParcelOtherCleanTime;
            client.OnParcelBuy += ProcessParcelBuy;
        }

        public virtual void SubscribeToClientGridEvents(IClientAPI client)
        {
            //client.OnNameFromUUIDRequest += HandleUUIDNameRequest;
            client.OnMoneyTransferRequest += ProcessMoneyTransferRequest;
        }

        public virtual void SubscribeToClientNetworkEvents(IClientAPI client)
        {
            client.OnNetworkStatsUpdate += StatsReporter.AddPacketsStats;
            client.OnViewerEffect += ProcessViewerEffect;
        }

        /// <summary>
        /// Unsubscribe the client from events.
        /// </summary>
        /// FIXME: Not called anywhere!
        /// <param name="client">The IClientAPI of the client</param>
        public virtual void UnSubscribeToClientEvents(IClientAPI client)
        {
            UnSubscribeToClientTerrainEvents(client);
            UnSubscribeToClientPrimEvents(client);
            UnSubscribeToClientPrimRezEvents(client);
            UnSubscribeToClientInventoryEvents(client);
            UnSubscribeToClientTeleportEvents(client);
            UnSubscribeToClientScriptEvents(client);
            UnSubscribeToClientParcelEvents(client);
            UnSubscribeToClientGridEvents(client);
            UnSubscribeToClientNetworkEvents(client);
        }

        public virtual void UnSubscribeToClientTerrainEvents(IClientAPI client)
        {
//            client.OnRegionHandShakeReply -= SendLayerData;
        }

        public virtual void UnSubscribeToClientPrimEvents(IClientAPI client)
        {
            client.OnUpdatePrimGroupPosition -= m_sceneGraph.UpdatePrimGroupPosition;
            client.OnUpdatePrimSinglePosition -= m_sceneGraph.UpdatePrimSinglePosition;

            client.onClientChangeObject -= m_sceneGraph.ClientChangeObject;

            client.OnUpdatePrimGroupRotation -= m_sceneGraph.UpdatePrimGroupRotation;
            client.OnUpdatePrimGroupMouseRotation -= m_sceneGraph.UpdatePrimGroupRotation;
            client.OnUpdatePrimSingleRotation -= m_sceneGraph.UpdatePrimSingleRotation;
            client.OnUpdatePrimSingleRotationPosition -= m_sceneGraph.UpdatePrimSingleRotationPosition;

            client.OnUpdatePrimScale -= m_sceneGraph.UpdatePrimScale;
            client.OnUpdatePrimGroupScale -= m_sceneGraph.UpdatePrimGroupScale;
            client.OnUpdateExtraParams -= m_sceneGraph.UpdateExtraParam;
            client.OnUpdatePrimShape -= m_sceneGraph.UpdatePrimShape;
            client.OnUpdatePrimTexture -= m_sceneGraph.UpdatePrimTexture;
            client.OnObjectRequest -= RequestPrim;
            client.OnObjectSelect -= SelectPrim;
            client.OnObjectDeselect -= DeselectPrim;
            client.OnDeRezObject -= DeRezObjects;
            client.OnObjectName -= m_sceneGraph.PrimName;
            client.OnObjectClickAction -= m_sceneGraph.PrimClickAction;
            client.OnObjectMaterial -= m_sceneGraph.PrimMaterial;
            client.OnLinkObjects -= LinkObjects;
            client.OnDelinkObjects -= DelinkObjects;
            client.OnObjectDuplicate -= DuplicateObject;
            client.OnObjectDuplicateOnRay -= doObjectDuplicateOnRay;
            client.OnUpdatePrimFlags -= m_sceneGraph.UpdatePrimFlags;
            client.OnRequestObjectPropertiesFamily -= m_sceneGraph.RequestObjectPropertiesFamily;
            client.OnObjectPermissions -= HandleObjectPermissionsUpdate;
            client.OnGrabObject -= ProcessObjectGrab;
            client.OnGrabUpdate -= ProcessObjectGrabUpdate;
            client.OnDeGrabObject -= ProcessObjectDeGrab;
            client.OnSpinStart -= ProcessSpinStart;
            client.OnSpinUpdate -= ProcessSpinObject;
            client.OnSpinStop -= ProcessSpinObjectStop;
            client.OnUndo -= m_sceneGraph.HandleUndo;
            client.OnRedo -= m_sceneGraph.HandleRedo;
            client.OnObjectDescription -= m_sceneGraph.PrimDescription;
            client.OnObjectIncludeInSearch -= m_sceneGraph.MakeObjectSearchable;
            client.OnObjectOwner -= ObjectOwner;
        }

        public virtual void UnSubscribeToClientPrimRezEvents(IClientAPI client)
        {
            client.OnAddPrim -= AddNewPrim;
            client.OnRezObject -= RezObject;
        }

        public virtual void UnSubscribeToClientInventoryEvents(IClientAPI client)
        {
            client.OnCreateNewInventoryFolder -= HandleCreateInventoryFolder;
            client.OnUpdateInventoryFolder -= HandleUpdateInventoryFolder;
            client.OnMoveInventoryFolder -= HandleMoveInventoryFolder; // 2; //!!
            client.OnFetchInventoryDescendents -= HandleFetchInventoryDescendents;
            client.OnPurgeInventoryDescendents -= HandlePurgeInventoryDescendents; // 2; //!!
            client.OnFetchInventory -= m_asyncInventorySender.HandleFetchInventory;
            client.OnUpdateInventoryItem -= UpdateInventoryItem;
            client.OnCopyInventoryItem -= CopyInventoryItem;
            client.OnMoveInventoryItem -= MoveInventoryItem;
            client.OnRemoveInventoryItem -= RemoveInventoryItem;
            client.OnRemoveInventoryFolder -= RemoveInventoryFolder;
            client.OnRezScript -= RezScript;
            client.OnRequestTaskInventory -= RequestTaskInventory;
            client.OnRemoveTaskItem -= RemoveTaskInventory;
            client.OnUpdateTaskInventory -= UpdateTaskInventory;
            client.OnMoveTaskItem -= ClientMoveTaskInventoryItem;
        }

        public virtual void UnSubscribeToClientTeleportEvents(IClientAPI client)
        {
            client.OnTeleportLocationRequest -= RequestTeleportLocation;
            //client.OnTeleportLandmarkRequest -= RequestTeleportLandmark;
            //client.OnTeleportHomeRequest -= TeleportClientHome;
        }

        public virtual void UnSubscribeToClientScriptEvents(IClientAPI client)
        {
            client.OnScriptReset -= ProcessScriptReset;
            client.OnGetScriptRunning -= GetScriptRunning;
            client.OnSetScriptRunning -= SetScriptRunning;
        }

        public virtual void UnSubscribeToClientParcelEvents(IClientAPI client)
        {
            client.OnParcelReturnObjectsRequest -= LandChannel.ReturnObjectsInParcel;
            client.OnParcelSetOtherCleanTime -= LandChannel.SetParcelOtherCleanTime;
            client.OnParcelBuy -= ProcessParcelBuy;
        }

        public virtual void UnSubscribeToClientGridEvents(IClientAPI client)
        {
            //client.OnNameFromUUIDRequest -= HandleUUIDNameRequest;
            client.OnMoneyTransferRequest -= ProcessMoneyTransferRequest;
        }

        public virtual void UnSubscribeToClientNetworkEvents(IClientAPI client)
        {
            client.OnNetworkStatsUpdate -= StatsReporter.AddPacketsStats;
            client.OnViewerEffect -= ProcessViewerEffect;
        }

        /// <summary>
        /// Teleport an avatar to their home region
        /// </summary>
        /// <param name="agentId">The avatar's Unique ID</param>
        /// <param name="client">The IClientAPI for the client</param>
        public virtual bool TeleportClientHome(UUID agentId, IClientAPI client)
        {
            if (EntityTransferModule is not null)
            {
                return EntityTransferModule.TeleportHome(agentId, client);
            }
            else
            {
                m_log.DebugFormat("[SCENE]: Unable to teleport user home: no AgentTransferModule is active");
                client.SendTeleportFailed("Unable to perform teleports on this simulator.");
            }
            return false;
        }

        /// <summary>
        /// Duplicates object specified by localID. This is the event handler for IClientAPI.
        /// </summary>
        /// <param name="originalPrim">ID of object to duplicate</param>
        /// <param name="offset"></param>
        /// <param name="flags"></param>
        /// <param name="AgentID">Agent doing the duplication</param>
        /// <param name="GroupID">Group of new object</param>
        public void DuplicateObject(uint originalPrim, Vector3 offset, uint flags, UUID AgentID, UUID GroupID)
        {
            bool createSelected = (flags & (uint)PrimFlags.CreateSelected) != 0;
            SceneObjectGroup copy = SceneGraph.DuplicateObject(originalPrim, offset, AgentID,
                    GroupID, Quaternion.Identity, createSelected);
            if (copy is not null)
                EventManager.TriggerObjectAddedToScene(copy);
        }

        /// <summary>
        /// Duplicates object specified by localID at position raycasted against RayTargetObject using
        /// RayEnd and RayStart to determine what the angle of the ray is
        /// </summary>
        /// <param name="localID">ID of object to duplicate</param>
        /// <param name="dupeFlags"></param>
        /// <param name="AgentID">Agent doing the duplication</param>
        /// <param name="GroupID">Group of new object</param>
        /// <param name="RayTargetObj">The target of the Ray</param>
        /// <param name="RayEnd">The ending of the ray (farthest away point)</param>
        /// <param name="RayStart">The Beginning of the ray (closest point)</param>
        /// <param name="BypassRaycast">Bool to bypass raycasting</param>
        /// <param name="RayEndIsIntersection">The End specified is the place to add the object</param>
        /// <param name="CopyCenters">Position the object at the center of the face that it's colliding with</param>
        /// <param name="CopyRotates">Rotate the object the same as the localID object</param>
        public void doObjectDuplicateOnRay(uint localID, uint dupeFlags, UUID AgentID, UUID GroupID,
                                           UUID RayTargetObj, Vector3 RayEnd, Vector3 RayStart,
                                           bool BypassRaycast, bool RayEndIsIntersection, bool CopyCenters, bool CopyRotates)
        {
            Vector3 pos;
            const bool frontFacesOnly = true;
            //m_log.Info("HITTARGET: " + RayTargetObj.ToString() + ", COPYTARGET: " + localID.ToString());
            SceneObjectPart target = GetSceneObjectPart(localID);
            SceneObjectPart target2 = GetSceneObjectPart(RayTargetObj);

            bool createSelected = (dupeFlags & (uint)PrimFlags.CreateSelected) != 0;

            if (target is not null && target2 is not null)
            {
                Vector3 direction = RayEnd - RayStart;
                direction.Normalize();

                //pos = target2.AbsolutePosition;
                //m_log.Info("[OBJECT_REZ]: TargetPos: " + pos.ToString() + ", RayStart: " + RayStart.ToString() + ", RayEnd: " + RayEnd.ToString() + ", Volume: " + Util.GetDistanceTo(RayStart,RayEnd).ToString() + ", mag1: " + Util.GetMagnitude(RayStart).ToString() + ", mag2: " + Util.GetMagnitude(RayEnd).ToString());

                // TODO: Raytrace better here

                //EntityIntersection ei = m_sceneGraph.GetClosestIntersectingPrim(new Ray(AXOrigin, AXdirection));
                Ray NewRay = new(RayStart, direction);

                // Ray Trace against target here
                EntityIntersection ei = target2.TestIntersectionOBB(NewRay, Quaternion.Identity, frontFacesOnly, CopyCenters);

                // Un-comment out the following line to Get Raytrace results printed to the console.
                //m_log.Info("[RAYTRACERESULTS]: Hit:" + ei.HitTF.ToString() + " Point: " + ei.ipoint.ToString() + " Normal: " + ei.normal.ToString());
                float ScaleOffset = 0.5f;

                // If we hit something
                if (ei.HitTF)
                {
                    Vector3 scale = target.Scale;
                    Vector3 scaleComponent = ei.AAfaceNormal;
                    if (scaleComponent.X != 0) ScaleOffset = scale.X;
                    if (scaleComponent.Y != 0) ScaleOffset = scale.Y;
                    if (scaleComponent.Z != 0) ScaleOffset = scale.Z;
                    ScaleOffset = Math.Abs(ScaleOffset);
                    Vector3 intersectionpoint = ei.ipoint;
                    Vector3 normal = ei.normal;
                    Vector3 offset = normal * (ScaleOffset / 2f);
                    pos = intersectionpoint + offset;

                    // stick in offset format from the original prim
                    pos -= target.ParentGroup.AbsolutePosition;
                    SceneObjectGroup copy;
                    if (CopyRotates)
                    {
                        Quaternion worldRot = target2.GetWorldRotation();

                        // SceneObjectGroup obj = m_sceneGraph.DuplicateObject(localID, pos, target.GetEffectiveObjectFlags(), AgentID, GroupID, worldRot);
                        copy = m_sceneGraph.DuplicateObject(localID, pos, AgentID, GroupID, worldRot, createSelected);
                        //obj.Rotation = worldRot;
                        //obj.UpdateGroupRotationR(worldRot);
                    }
                    else
                    {
                        copy = m_sceneGraph.DuplicateObject(localID, pos, AgentID, GroupID, Quaternion.Identity, createSelected);
                    }

                    if (copy is not null)
                        EventManager.TriggerObjectAddedToScene(copy);
                }
            }
        }

        /// <summary>
        /// Get the avatar appearance for the given client.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="appearance"></param>
        public void GetAvatarAppearance(IClientAPI client, out AvatarAppearance appearance)
        {
            AgentCircuitData aCircuit = m_authenticateHandler.GetAgentCircuitData(client.CircuitCode);

            if (aCircuit is null)
            {
                m_log.DebugFormat("[APPEARANCE] Client did not supply a circuit. Non-Linden? Creating default appearance.");
                appearance = new AvatarAppearance();
                return;
            }

            appearance = aCircuit.Appearance;
            if (appearance is null)
            {
                m_log.DebugFormat("[APPEARANCE]: Appearance not found in {0}, returning default", RegionInfo.RegionName);
                appearance = new AvatarAppearance();
            }
        }

        /// <summary>
        /// Remove the given client from the scene.
        /// </summary>
        /// <remarks>
        /// Only clientstack code should call this directly.  All other code should call IncomingCloseAgent() instead
        /// to properly operate the state machine and avoid race conditions with other close requests (such as directly
        /// from viewers).
        /// </remarks>
        /// <param name='agentID'>ID of agent to close</param>
        /// <param name='closeChildAgents'>
        /// Close the neighbour child agents associated with this client.
        /// </param>
        ///

        private readonly object m_removeClientPrivLock = new();

        public void RemoveClient(UUID agentID, bool closeChildAgents)
        {
            AgentCircuitData acd = m_authenticateHandler.GetAgentCircuitData(agentID);

            // Shouldn't be necessary since RemoveClient() is currently only called by IClientAPI.Close() which
            // in turn is only called by Scene.IncomingCloseAgent() which checks whether the presence exists or not
            // However, will keep for now just in case.
            if (acd is null)
            {
                m_log.ErrorFormat(
                    "[SCENE]: No agent circuit found for {0} in {1}, aborting Scene.RemoveClient", agentID, Name);

                return;
            }

            // TODO: Can we now remove this lock?
            lock (m_removeClientPrivLock)
            {
                bool isChildAgent = false;

                ScenePresence avatar = GetScenePresence(agentID);

                // Shouldn't be necessary since RemoveClient() is currently only called by IClientAPI.Close() which
                // in turn is only called by Scene.IncomingCloseAgent() which checks whether the presence exists or not
                // However, will keep for now just in case.
                if (avatar is null)
                {
                    m_log.ErrorFormat(
                        "[SCENE]: Called RemoveClient() with agent ID {0} but no such presence is in the scene.", agentID);
                    m_authenticateHandler.RemoveCircuit(agentID);

                    return;
                }

                try
                {
                    isChildAgent = avatar.IsChildAgent;

                    m_log.DebugFormat(
                        "[SCENE]: Removing {0} agent {1} {2} from {3}",
                        isChildAgent ? "child" : "root", avatar.Name, agentID, Name);

                    // Don't do this to root agents, it's not nice for the viewer
                    if (closeChildAgents && isChildAgent)
                    {
                        // Tell a single agent to disconnect from the region.
                        // Let's do this via UDP
                        avatar.ControllingClient.SendShutdownConnectionNotice();
                    }

                    // Only applies to root agents.
                    if (avatar.ParentID != 0)
                    {
                        avatar.StandUp();
                    }

                    m_sceneGraph.removeUserCount(!isChildAgent);

                    if (closeChildAgents && !isChildAgent)
                    {
                        List<ulong> regions = avatar.KnownRegionHandles;
                        regions.Remove(RegionInfo.RegionHandle);

                        // This ends up being done asynchronously so that a logout isn't held up where there are many present but unresponsive neighbours.
                        m_sceneGridService.SendCloseChildAgentConnections(agentID, acd.SessionID.ToString(), regions);
                    }

                    m_eventManager.TriggerClientClosed(agentID, this);
//                    m_log.Debug("[Scene]TriggerClientClosed done");
                    m_eventManager.TriggerOnRemovePresence(agentID);
//                    m_log.Debug("[Scene]TriggerOnRemovePresence done");

                    if (!isChildAgent)
                    {
                        AttachmentsModule?.DeRezAttachments(avatar);

                        ForEachClient(
                            delegate(IClientAPI client)
                            {
                                //We can safely ignore null reference exceptions.  It means the avatar is dead and cleaned up anyway
                                try { client.SendKillObject(new List<uint> { avatar.LocalId }); }
                                catch (NullReferenceException) { }
                            });
                    }

                    // It's possible for child agents to have transactions if changes are being made cross-border.
                    //   m_log.Debug("[Scene]RemoveAgentAssetTransactions");
                    AgentTransactionsModule?.RemoveAgentAssetTransactions(agentID);
                    m_log.Debug("[Scene] The avatar has left the building");
                }
                catch (Exception e)
                {
                    m_log.Error(
                        string.Format("[SCENE]: Exception removing {0} from {1}.  Cleaning up.  Exception ", avatar.Name, Name), e);
                }
                finally
                {
                    try
                    {
                        // Always clean these structures up so that any failure above doesn't cause them to remain in the
                        // scene with possibly bad effects (e.g. continually timing out on unacked packets and triggering
                        // the same cleanup exception continually.
                        m_authenticateHandler.RemoveCircuit(acd);
                        m_sceneGraph.RemoveScenePresence(agentID);
                        m_clientManager.Remove(agentID);
                        if (m_capsModule is not null)
                        {
                            if(avatar is null || !avatar.IsNPC)
                                m_capsModule.RemoveCaps(agentID, acd.circuitcode);
                        }
                        avatar.Dispose();
                    }
                    catch (Exception e)
                    {
                        m_log.Error(
                            string.Format("[SCENE]: Exception in final clean up of {0} in {1}.  Exception ", avatar.Name, Name), e);
                    }
                }
            }

            //m_log.InfoFormat("[SCENE] Memory pre  GC {0}", System.GC.GetTotalMemory(false));
            //m_log.InfoFormat("[SCENE] Memory post GC {0}", System.GC.GetTotalMemory(true));
        }

        /// <summary>
        /// Removes region from an avatar's known region list.  This coincides with child agents.  For each child agent, there will be a known region entry.
        ///
        /// </summary>
        /// <param name="avatarID"></param>
        /// <param name="regionslst"></param>
        public void HandleRemoveKnownRegionsFromAvatar(UUID avatarID, List<ulong> regionslst)
        {
            ScenePresence av = GetScenePresence(avatarID);
            if (av is not null)
            {
                lock (av)
                {
                    for (int i = 0; i < regionslst.Count; i++)
                    {
                        av.RemoveNeighbourRegion(regionslst[i]);
                    }
                }
            }
        }

        #endregion

        #region Entities

        public void SendKillObject(List<uint> localIDs)
        {
            List<uint> deleteIDs = new();

            foreach (uint localID in localIDs)
            {
                SceneObjectPart part = GetSceneObjectPart(localID);
                if (part is not null && part.ParentGroup is not null &&
                            part.ParentGroup.RootPart == part)
                    deleteIDs.Add(localID);
            }

            ForEachClient(c => c.SendKillObject(deleteIDs));
        }

        #endregion

        #region RegionComms

        /// <summary>
        /// Do the work necessary to initiate a new user connection for a particular scene.
        /// </summary>
        /// <param name="agent">CircuitData of the agent who is connecting</param>
        /// <param name="teleportFlags"></param>
        /// <param name="source">Source region (may be null)</param>
        /// <param name="reason">Outputs the reason for the false response on this string</param>
        /// <returns>True if the region accepts this agent.  False if it does not.  False will
        /// also return a reason.</returns>
        public bool NewUserConnection(AgentCircuitData agent, uint teleportFlags, GridRegion source, out string reason)
        {
            return NewUserConnection(agent, teleportFlags, source, out reason, true);
        }

        /// <summary>
        /// Do the work necessary to initiate a new user connection for a particular scene.
        /// </summary>
        /// <remarks>
        /// The return bool should allow for connections to be refused, but as not all calling paths
        /// take proper notice of it yet, we still allowed banned users in.
        ///
        /// At the moment this method consists of setting up the caps infrastructure
        /// The return bool should allow for connections to be refused, but as not all calling paths
        /// take proper notice of it let, we allowed banned users in still.
        ///
        /// This method is called by the login service (in the case of login) or another simulator (in the case of region
        /// cross or teleport) to initiate the connection.  It is not triggered by the viewer itself - the connection
        /// is activated later when the viewer sends the initial UseCircuitCodePacket UDP packet (in the case of
        /// the LLUDP stack).
        /// </remarks>
        /// <param name="acd">CircuitData of the agent who is connecting</param>
        /// <param name="source">Source region (may be null)</param>
        /// <param name="reason">Outputs the reason for the false response on this string</param>
        /// <param name="requirePresenceLookup">True for normal presence. False for NPC
        /// or other applications where a full grid/Hypergrid presence may not be required.</param>
        /// <returns>True if the region accepts this agent.  False if it does not.  False will
        /// also return a reason.</returns>
        ///
        private readonly object m_newUserConnLock = new();

        public bool NewUserConnection(AgentCircuitData acd, uint teleportFlags, GridRegion source, out string reason, bool requirePresenceLookup)
        {
            bool vialogin = (teleportFlags & (uint)(TPFlags.ViaLogin | TPFlags.ViaHGLogin)) != 0;
            bool viahome = (teleportFlags & (uint)TPFlags.ViaHome) != 0;
            //bool godlike = ((teleportFlags & (uint)TPFlags.Godlike) != 0);

            reason = String.Empty;

            //Teleport flags:
            //
            // TeleportFlags.ViaGodlikeLure - Border Crossing
            // TeleportFlags.ViaLogin - Login
            // TeleportFlags.ViaLure - Teleport request sent by another user
            // TeleportFlags.ViaLandmark | TeleportFlags.ViaLocation | TeleportFlags.ViaLandmark | TeleportFlags.Default - Regular Teleport

            // Don't disable this log message - it's too helpful
            string curViewer = Util.GetViewerName(acd);
            m_log.DebugFormat(
                "[SCENE]: Region {0} told of incoming {1} agent {2} {3} {4} (circuit code {5}, IP {6}, viewer {7}, teleportflags ({8}), position {9}. {10}",
                RegionInfo.RegionName,
                (acd.child ? "child" : "root"),
                acd.firstname,
                acd.lastname,
                acd.AgentID,
                acd.circuitcode,
                acd.IPAddress,
                curViewer,
                ((TPFlags)teleportFlags).ToString(),
                acd.startpos,
                (source is null) ? "" : string.Format("From region {0} ({1}){2}", source.RegionName, source.RegionID, (source.RawServerURI is null) ? "" : " @ " + source.ServerURI)
            );

            //m_log.DebugFormat("NewUserConnection stack {0}", Environment.StackTrace);

            if (!LoginsEnabled)
            {
                reason = "Logins to this region are disabled";
                return false;
            }

            //Check if the viewer is banned or in the viewer access list
            //We check if the substring is listed for higher flexebility
            bool ViewerDenied = true;
            string cV = null;

            //Check if the specific viewer is listed in the allowed viewer list
            if (m_AllowedViewers.Count > 0)
            {
                cV = curViewer.Trim().ToLower();
                foreach (string viewer in m_AllowedViewers)
                {
                    if (viewer == cV[..Math.Min(viewer.Length, curViewer.Length)])
                    {
                        ViewerDenied = false;
                        break;
                    }
                }
            }
            else
            {
                ViewerDenied = false;
            }

            //Check if the viewer is in the banned list
            if (m_BannedViewers.Count > 0)
            {
                cV ??= curViewer.Trim().ToLower();
                foreach (string viewer in m_BannedViewers)
                {
                    if (viewer == cV[..Math.Min(viewer.Length, curViewer.Length)])
                    {
                        ViewerDenied = true;
                        break;
                    }
                }
            }

            if (ViewerDenied)
            {
                m_log.DebugFormat(
                    "[SCENE]: Access denied for {0} {1} using {2}",
                    acd.firstname, acd.lastname, curViewer);
                reason = "Access denied, your viewer is banned";
                return false;
            }

            ScenePresence sp;

            lock (m_removeClientLock)
            {
                sp = GetScenePresence(acd.AgentID);

                // We need to ensure that we are not already removing the scene presence before we ask it not to be
                // closed.
                if (sp is not null && !sp.IsDeleted && sp.IsChildAgent &&
                    (sp.LifecycleState == ScenePresenceState.Running || sp.LifecycleState == ScenePresenceState.PreRemove))
                {
                    m_log.Debug($"[SCENE]: Reusing existing child scene presence for {sp.Name}, state {sp.LifecycleState} in {Name}");

                    // In the case where, for example, an A B C D region layout, an avatar may
                    // teleport from A -> D, but then -> C before A has asked B to close its old child agent.  When C
                    // renews the lease on the child agent at B, we must make sure that the close from A does not succeed.
                    //
                    // XXX: In the end, this should not be necessary if child agents are closed without delay on
                    // teleport, since realistically, the close request should always be processed before any other
                    // region tried to re-establish a child agent.  This is much simpler since the logic below is
                    // vulnerable to an issue when a viewer quits a region without sending a proper logout but then
                    // re-establishes the connection on a relogin.  This could wrongly set the DoNotCloseAfterTeleport
                    // flag when no teleport had taken place (and hence no close was going to come).

                    //if (!acd.ChildrenCapSeeds.ContainsKey(RegionInfo.RegionHandle))
                    //{
                    //    m_log.DebugFormat(
                    //        "[SCENE]: Setting DoNotCloseAfterTeleport for child scene presence {0} in {1} because source will attempt close.",
                    //        sp.Name, Name);

                    //    sp.DoNotCloseAfterTeleport = true;
                    //}
                    //else if (EntityTransferModule.IsInTransit(sp.UUID))

                    sp.LifecycleState = ScenePresenceState.Running;

                    if (EntityTransferModule.IsInTransit(sp.UUID))
                    {
                        sp.DoNotCloseAfterTeleport = true;
                        m_log.Debug($"[SCENE]: Set DoNotCloseAfterTeleport for child scene presence {sp.Name} in {Name} because this region will attempt end-of-teleport close from a previous close.");
                    }
                }
            }

            // Need to poll here in case we are currently deleting an sp.  Letting threads run over each other will
            // allow unpredictable things to happen.
            if (sp is not null && sp.LifecycleState == ScenePresenceState.Removing)
            {
                const int polls = 10;
                const int pollInterval = 1000;
                int pollsLeft = polls;

                try
                {
                    while (!sp.IsDeleted && sp.LifecycleState == ScenePresenceState.Removing && pollsLeft-- > 0)
                        Thread.Sleep(pollInterval);

                    if (!sp.IsDeleted && sp.LifecycleState == ScenePresenceState.Removing)
                    {
                        m_log.WarnFormat(
                            "[SCENE]: Agent {0} in {1} was still being removed after {2}s.  Aborting NewUserConnection.",
                            sp.Name, Name, polls * pollInterval / 1000);

                        return false;
                    }
                    else if (polls != pollsLeft)
                    {
                        m_log.DebugFormat(
                            "[SCENE]: NewUserConnection for agent {0} in {1} had to wait {2}s for in-progress removal to complete on an old presence.",
                            sp.Name, Name, polls * pollInterval / 1000);
                    }
                }
                catch
                {
                    sp = null;
                }
            }

            if (teleportFlags != (uint)TPFlags.Default)
            {
                // Make sure root avatar position is in the region
                if (acd.startpos.X < 0)
                    acd.startpos.X = 1f;
                else if (acd.startpos.X >= RegionInfo.RegionSizeX)
                    acd.startpos.X = RegionInfo.RegionSizeX - 1f;
                if (acd.startpos.Y < 0)
                    acd.startpos.Y = 1f;
                else if (acd.startpos.Y >= RegionInfo.RegionSizeY)
                    acd.startpos.Y = RegionInfo.RegionSizeY - 1f;
            }

            // only check access, actual relocations will happen later on ScenePresence MakeRoot
            // allow child agents creation
            //if(!godlike && teleportFlags != (uint) TPFlags.Default)
            if (teleportFlags != (uint)TPFlags.Default)
            {
                bool checkTeleHub;

                // don't check hubs if via home or via lure
                if ((teleportFlags & (uint)(TPFlags.ViaHome | TPFlags.ViaLure)) != 0)
                    checkTeleHub = false;
                else
                    checkTeleHub = vialogin
                        || (TelehubAllowLandmarks != true && ((teleportFlags & (uint)(TPFlags.ViaLandmark | TPFlags.ViaLocation)) != 0));

                if (!CheckLandPositionAccess(acd.AgentID, true, checkTeleHub, false, acd.startpos, out reason))
                {
                    m_authenticateHandler.RemoveCircuit(acd);
                    return false;
                }
            }

            // TODO: can we remove this lock?
            lock (m_newUserConnLock)
            {
                if(sp is not null && sp.IsDeleted)
                    sp = null; 

                if (sp is not null && !sp.IsChildAgent)
                {
                    // We have a root agent. Is it in transit?
                    if (!EntityTransferModule.IsInTransit(sp.UUID))
                    {
                        // We have a zombie from a crashed session.
                        // Or the same user is trying to be root twice here, won't work.
                        // Kill it.
                        m_log.WarnFormat(
                            "[SCENE]: Existing root scene presence detected for {0} {1} in {2} when connecting.  Removing existing presence.",
                            sp.Name, sp.UUID, RegionInfo.RegionName);

                        if (sp.ControllingClient is not null)
                            CloseAgent(sp.UUID, true);

                        sp = null;
                    }
                    //else
                    //    m_log.WarnFormat("[SCENE]: Existing root scene presence for {0} {1} in {2}, but agent is in trasit", sp.Name, sp.UUID, RegionInfo.RegionName);
                }

                // Optimistic: add or update the circuit data with the new agent circuit data and teleport flags.
                // We need the circuit data here for some of the subsequent checks. (groups, for example)
                // If the checks fail, we remove the circuit.
                acd.teleportFlags = teleportFlags;

                if (vialogin)
                {
                    IUserAccountCacheModule cache = RequestModuleInterface<IUserAccountCacheModule>();
                    cache?.Remove(acd.AgentID);
                }

                m_authenticateHandler.AddNewCircuit(acd);

                if (sp is null) // We don't have an [child] agent here already
                {
                    if (requirePresenceLookup)
                    {
                        try
                        {
                            if (!VerifyUserPresence(acd, out reason))
                            {
                                m_authenticateHandler.RemoveCircuit(acd);
                                return false;
                            }
                        }
                        catch (Exception e)
                        {
                            m_log.ErrorFormat(
                                "[SCENE]: Exception verifying presence {0}{1}", e.Message, e.StackTrace);

                            m_authenticateHandler.RemoveCircuit(acd);
                            return false;
                        }
                    }

                    try
                    {
                        if (!AuthorizeUser(acd, (!vialogin && SeeIntoRegion), out reason))
                        {
                            m_authenticateHandler.RemoveCircuit(acd);
                            return false;
                        }
                    }
                    catch (Exception e)
                    {
                        m_log.ErrorFormat(
                            "[SCENE]: Exception authorizing user {0}{1}", e.Message, e.StackTrace);

                        m_authenticateHandler.RemoveCircuit(acd);
                        return false;
                    }

                    m_log.InfoFormat(
                        "[SCENE {0}]: authorized {1} agent {2} {3} {4} (circuit code {5})",
                        Name, (acd.child ? "child" : "root"), acd.firstname, acd.lastname,
                        acd.AgentID, acd.circuitcode);


                    if (m_capsModule is not null)
                    {
                        m_capsModule.SetAgentCapsSeeds(acd);
                        m_capsModule.CreateCaps(acd.AgentID, acd.circuitcode);
                    }
                }
                else
                {
                    sp.TeleportFlags = (TPFlags)teleportFlags;

                    if (sp.IsChildAgent)
                    {
                        m_log.DebugFormat(
                            "[SCENE]: Adjusting known seeds for existing agent {0} in {1}",
                            acd.AgentID, RegionInfo.RegionName);

                        if (m_capsModule is not null)
                        {
                            m_capsModule.SetAgentCapsSeeds(acd);
                            m_capsModule.CreateCaps(acd.AgentID, acd.circuitcode);
                        }

                        sp.AdjustKnownSeeds();
                    }
                }

                // Try caching an incoming user name much earlier on to see if this helps with an issue
                // where HG users are occasionally seen by others as "Unknown User" because their UUIDName
                // request for the HG avatar appears to trigger before the user name is cached.
                CacheUserName(null, acd);
            }

            m_capsModule?.ActivateCaps(acd.circuitcode);

            return true;
        }

        private bool IsPositionAllowed(UUID agentID, Vector3 pos, ref string reason)
        {
            ILandObject land = LandChannel.GetLandObject(pos);
            if (land is null)
                return true;

            if (land.IsBannedFromLand(agentID) || land.IsRestrictedFromLand(agentID))
            {
                reason = "You are banned from the region.";
                return false;
            }

            return true;
        }

        public bool TestLandRestrictions(UUID agentID, out string reason, ref float posX, ref float posY)
        {
            if (posX < 0)
                posX = 0;

            else if (posX >= RegionInfo.RegionSizeX)
                posX = RegionInfo.RegionSizeX - 0.5f;
            if (posY < 0)
                posY = 0;
            else if (posY >= RegionInfo.RegionSizeY)
                posY = RegionInfo.RegionSizeY - 0.5f;

            reason = String.Empty;
            if (Permissions.IsGod(agentID))
                return true;

            ILandObject land = LandChannel.GetLandObject(posX, posY);
            if (land is null)
                return false;

            bool banned = land.IsBannedFromLand(agentID);
            bool restricted = land.IsRestrictedFromLand(agentID);

            if (banned || restricted)
            {
                ILandObject nearestParcel = GetNearestAllowedParcel(agentID, posX, posY);
                Vector2? newPosition = null;
                if (nearestParcel is not null)
                {
                    //Move agent to nearest allowed
//                    Vector2 newPosition = GetParcelSafeCorner(nearestParcel);
                    newPosition = nearestParcel.GetNearestPoint(new Vector3(posX, posY,0));
                }
                if(newPosition is null)
                {
                    if (banned)
                    {
                        reason = "Cannot regioncross into banned parcel.";
                    }
                    else
                    {
                        reason = String.Format("Denied access to private region {0}: You are not on the access list for that region.",
                            RegionInfo.RegionName);
                    }
                    return false;
                }
                else
                {
                    posX = newPosition.Value.X;
                    posY = newPosition.Value.Y;
                }
            }
            reason = "";
            return true;
        }

        /// <summary>
        /// Verifies that the user has a presence on the Grid
        /// </summary>
        /// <param name="agent">Circuit Data of the Agent we're verifying</param>
        /// <param name="reason">Outputs the reason for the false response on this string</param>
        /// <returns>True if the user has a session on the grid.  False if it does not.  False will
        /// also return a reason.</returns>
        public virtual bool VerifyUserPresence(AgentCircuitData agent, out string reason)
        {
            IPresenceService presencesvc = RequestModuleInterface<IPresenceService>();
            if (presencesvc is null)
            {
                reason = String.Format("Failed to verify user presence in the grid for {0} {1} in region {2}. Presence service does not exist.", agent.firstname, agent.lastname, RegionInfo.RegionName);
                return false;
            }

            OpenSim.Services.Interfaces.PresenceInfo pinfo = presencesvc.GetAgent(agent.SessionID);

            if (pinfo is null)
            {
                reason = String.Format("Failed to verify user presence in the grid for {0} {1}, access denied to region {2}.", agent.firstname, agent.lastname, RegionInfo.RegionName);
                return false;
            }

            if(pinfo.UserID != agent.AgentID.ToString())
            {
                reason = String.Format("Failed to verify user presence in the grid for {0} {1}, access denied to region {2}.", agent.firstname, agent.lastname, RegionInfo.RegionName);
                return false;
            }

            reason = string.Empty;
            return true;
        }

        /// <summary>
        /// Verify if the user can connect to this region.  Checks the banlist and ensures that the region is set for public access
        /// </summary>
        /// <param name="agent">The circuit data for the agent</param>
        /// <param name="reason">outputs the reason to this string</param>
        /// <returns>True if the region accepts this agent.  False if it does not.  False will
        /// also return a reason.</returns>

        protected virtual bool AuthorizeUser(AgentCircuitData agent, bool bypassAccessControl, out string reason)
        {
            reason = string.Empty;
            if(agent.AgentID.Equals(Constants.servicesGodAgentID))
                return false;

            if (!m_strictAccessControl)
                return true;
            if (Permissions.IsGod(agent.AgentID))
                return true;

            if (AuthorizationService is not null)
            {
                if (!AuthorizationService.IsAuthorizedForRegion(
                    agent.AgentID.ToString(), agent.firstname, agent.lastname, RegionInfo.RegionID.ToString(), out reason))
                {
                    m_log.WarnFormat("[CONNECTION BEGIN]: Denied access to: {0} ({1} {2}) at {3} because: {4}",
                                     agent.AgentID, agent.firstname, agent.lastname, RegionInfo.RegionName, reason);
                    return false;
                }
            }

            // We only test the things below when we want to cut off
            // child agents from being present in the scene for which their root
            // agent isn't allowed. Otherwise, we allow child agents. The test for
            // the root is done elsewhere (QueryAccess)
            if (!bypassAccessControl)
            {
                if(RegionInfo.EstateSettings is null)
                {
                    // something is broken?  let it get in
                    m_log.ErrorFormat("[CONNECTION BEGIN]: Estate Settings is null!");
                    return true;
                }

                // check estate ban
                int flags = GetUserFlags(agent.AgentID);
                if (RegionInfo.EstateSettings.IsBanned(agent.AgentID, flags))
                {
                    m_log.WarnFormat("[CONNECTION BEGIN]: Denied access to: {0} ({1} {2}) at {3} because the user is on the banlist",
                            agent.AgentID, agent.firstname, agent.lastname, RegionInfo.RegionName);
                    reason = string.Format("Denied access to region {0}: You have been banned from that region.",
                            RegionInfo.RegionName);
                    return false;
                }

                // public access
                if (RegionInfo.EstateSettings.PublicAccess)
                    return true;

                // in access list / owner / manager
                if (RegionInfo.EstateSettings.HasAccess(agent.AgentID))
                    return true;

                // finally test groups
                bool groupAccess = false;

                // some say GOTO is ugly
                if(m_groupsModule is null) // if no groups refuse
                    goto Label_GroupsDone;

                UUID[] estateGroups = RegionInfo.EstateSettings.EstateGroups;

                if(estateGroups is null)
                {
                    m_log.ErrorFormat("[CONNECTION BEGIN]: Estate GroupMembership is null!");
                    goto Label_GroupsDone;
                }

                if(estateGroups.Length == 0)
                    goto Label_GroupsDone;

                List<UUID> agentGroups = new();
                GroupMembershipData[] GroupMembership = m_groupsModule.GetMembershipData(agent.AgentID);

                if(GroupMembership is null)
                {
                    m_log.ErrorFormat("[CONNECTION BEGIN]: GroupMembership is null!");
                    goto Label_GroupsDone;
                }

                if(GroupMembership.Length == 0)
                    goto Label_GroupsDone;

                for(int i = 0;i < GroupMembership.Length;i++)
                    agentGroups.Add(GroupMembership[i].GroupID);

                foreach(UUID group in estateGroups)
                {
                    if(agentGroups.Contains(group))
                    {
                        groupAccess = true;
                        break;
                    }
                }

Label_GroupsDone:
                if (!groupAccess)
                {
                    m_log.WarnFormat("[CONNECTION BEGIN]: Denied access to: {0} ({1} {2}) at {3} because the user does not have access to the estate",
                                     agent.AgentID, agent.firstname, agent.lastname, RegionInfo.RegionName);
                    reason = String.Format("Denied access to private region {0}: You do not have access to that region.",
                                     RegionInfo.RegionName);
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Update an AgentCircuitData object with new information
        /// </summary>
        /// <param name="data">Information to update the AgentCircuitData with</param>
        public void UpdateCircuitData(AgentCircuitData data)
        {
            m_authenticateHandler.UpdateAgentData(data);
        }

        /// <summary>
        /// Change the Circuit Code for the user's Circuit Data
        /// </summary>
        /// <param name="oldcc">The old Circuit Code.  Must match a previous circuit code</param>
        /// <param name="newcc">The new Circuit Code.  Must not be an already existing circuit code</param>
        /// <returns>True if we successfully changed it.  False if we did not</returns>
        public bool ChangeCircuitCode(uint oldcc, uint newcc)
        {
            return m_authenticateHandler.TryChangeCircuitCode(oldcc, newcc);
        }

//        /// <summary>
//        /// The Grid has requested that we log-off a user.  Log them off.
//        /// </summary>
//        /// <param name="AvatarID">Unique ID of the avatar to log-off</param>
//        /// <param name="RegionSecret">SecureSessionID of the user, or the RegionSecret text when logging on to the grid</param>
//        /// <param name="message">message to display to the user.  Reason for being logged off</param>
//        public void HandleLogOffUserFromGrid(UUID AvatarID, UUID RegionSecret, string message)
//        {
//            ScenePresence loggingOffUser = GetScenePresence(AvatarID);
//            if (loggingOffUser is not null)
//            {
//                UUID localRegionSecret = UUID.Zero;
//                bool parsedsecret = UUID.TryParse(RegionInfo.regionSecret, out localRegionSecret);
//
//                // Region Secret is used here in case a new sessionid overwrites an old one on the user server.
//                // Will update the user server in a few revisions to use it.
//
//                if (RegionSecret == loggingOffUser.ControllingClient.SecureSessionId || (parsedsecret && RegionSecret == localRegionSecret))
//                {
//                    m_sceneGridService.SendCloseChildAgentConnections(loggingOffUser.UUID, loggingOffUser.KnownRegionHandles);
//                    loggingOffUser.ControllingClient.Kick(message);
//                    // Give them a second to receive the message!
//                    Thread.Sleep(1000);
//                    loggingOffUser.ControllingClient.Close();
//                }
//                else
//                {
//                    m_log.Info("[USERLOGOFF]: System sending the LogOff user message failed to sucessfully authenticate");
//                }
//            }
//            else
//            {
//                m_log.InfoFormat("[USERLOGOFF]: Got a logoff request for {0} but the user isn't here.  The user might already have been logged out", AvatarID.ToString());
//            }
//        }

//        /// <summary>
//        /// Triggered when an agent crosses into this sim.  Also happens on initial login.
//        /// </summary>
//        /// <param name="agentID"></param>
//        /// <param name="position"></param>
//        /// <param name="isFlying"></param>
//        public virtual void AgentCrossing(UUID agentID, Vector3 position, bool isFlying)
//        {
//            ScenePresence presence = GetScenePresence(agentID);
//            if (presence is not null)
//            {
//                try
//                {
//                    presence.MakeRootAgent(position, isFlying);
//                }
//                catch (Exception e)
//                {
//                    m_log.ErrorFormat("[SCENE]: Unable to do agent crossing, exception {0}{1}", e.Message, e.StackTrace);
//                }
//            }
//            else
//            {
//                m_log.ErrorFormat(
//                    "[SCENE]: Could not find presence for agent {0} crossing into scene {1}",
//                    agentID, RegionInfo.RegionName);
//            }
//        }

        /// <summary>
        /// We've got an update about an agent that sees into this region,
        /// send it to ScenePresence for processing  It's the full data.
        /// </summary>
        /// <param name="cAgentData">Agent that contains all of the relevant things about an agent.
        /// Appearance, animations, position, etc.</param>
        /// <returns>true if we handled it.</returns>
        public virtual bool IncomingUpdateChildAgent(AgentData cAgentData)
        {
            m_log.DebugFormat(
                "[SCENE]: Incoming child agent update for {0} in {1}", cAgentData.AgentID, RegionInfo.RegionName);

            if (!LoginsEnabled)
            {
//                reason = "Logins Disabled";
                m_log.DebugFormat(
                    "[SCENE]: update for {0} in {1} refused: Logins Disabled", cAgentData.AgentID, RegionInfo.RegionName);
                return false;
            }

            int flags = GetUserFlags(cAgentData.AgentID);
            if (RegionInfo.EstateSettings.IsBanned(cAgentData.AgentID, flags))
            {
                m_log.DebugFormat("[SCENE]: Denying root agent entry to {0}: banned", cAgentData.AgentID);
                return false;
            }

            // TODO: This check should probably be in QueryAccess().
            ILandObject nearestParcel = GetNearestAllowedParcel(cAgentData.AgentID,
                (float)RegionInfo.RegionSizeX * 0.5f, (float)RegionInfo.RegionSizeY  * 0.5f);
            if (nearestParcel is null)
            {
                m_log.InfoFormat(
                    "[SCENE]: Denying root agent entry to {0} in {1}: no allowed parcel",
                    cAgentData.AgentID, RegionInfo.RegionName);

                return false;
            }

            // We have to wait until the viewer contacts this region
            // after receiving the EnableSimulator HTTP Event Queue message (for the v1 teleport protocol)
            // or TeleportFinish (for the v2 teleport protocol).  This triggers the viewer to send
            // a UseCircuitCode packet which in turn calls AddNewAgent which finally creates the ScenePresence.
            ScenePresence sp = WaitGetScenePresence(cAgentData.AgentID);

            if (sp is not null)
            {
                if (!sp.IsChildAgent)
                {
                    m_log.WarnFormat("[SCENE]: Ignoring a child update on a root agent {0} {1} in {2}",
                            sp.Name, sp.UUID, Name);
                    return false;
                }
                if (cAgentData.SessionID.NotEqual(sp.ControllingClient.SessionId))
                {
                    m_log.WarnFormat(
                        "[SCENE]: Attempt to update agent {0} with diferent session id {1} != {2}",
                        sp.UUID, sp.ControllingClient.SessionId, cAgentData.SessionID);
                    return false;
                }

                sp.UpdateChildAgent(cAgentData);

                int ntimes = 100;
                if (cAgentData.SenderWantsToWaitForRoot)
                {
                    while (sp.IsChildAgent && ntimes-- > 0)
                        Thread.Sleep(250);

                    if (sp.IsChildAgent)
                    {
                        m_log.WarnFormat(
                            "[SCENE]: Found presence {0} {1} unexpectedly still child in {2}",
                            sp.Name, sp.UUID, Name);
                        return false;
                    }
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// We've got an update about an agent that sees into this region,
        /// send it to ScenePresence for processing  It's only positional data
        /// </summary>
        /// <param name="cAgentData">AgentPosition that contains agent positional data so we can know what to send</param>
        /// <returns>true if we handled it.</returns>
        public virtual bool IncomingUpdateChildAgent(AgentPosition cAgentData)
        {
//            m_log.DebugFormat(
//                "[SCENE PRESENCE]: IncomingChildAgentDataUpdate POSITION for {0} in {1}, position {2}",
//                cAgentData.AgentID, Name, cAgentData.Position);

            ScenePresence childAgentUpdate = GetScenePresence(cAgentData.AgentID);
            if (childAgentUpdate is not null)
            {
//                if (childAgentUpdate.ControllingClient.SessionId != cAgentData.SessionID)
//                    // Only warn for now
//                    m_log.WarnFormat("[SCENE]: Attempt at updating position of agent {0} with invalid session id {1}. Neighbor running older version?",
//                        childAgentUpdate.UUID, cAgentData.SessionID);

                // I can't imagine *yet* why we would get an update if the agent is a root agent..
                // however to avoid a race condition crossing borders..
                if (childAgentUpdate.IsChildAgent)
                {
                    //Send Data to ScenePresence
                    childAgentUpdate.UpdateChildAgent(cAgentData);
                    // Not Implemented:
                    //TODO: Do we need to pass the message on to one of our neighbors?
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Poll until the requested ScenePresence appears or we timeout.
        /// </summary>
        /// <returns>The scene presence is found, else null.</returns>
        /// <param name='agentID'></param>
        protected virtual ScenePresence WaitGetScenePresence(UUID agentID)
        {
            int ntimes = 120; // 30s
            ScenePresence sp;
            while ((sp = GetScenePresence(agentID)) is null && (ntimes-- > 0))
                Thread.Sleep(250);

            if (sp is null)
                m_log.WarnFormat(
                    "[SCENE PRESENCE]: Did not find presence with id {0} in {1} before timeout",
                    agentID, RegionInfo.RegionName);

            return sp;
        }

        /// <summary>
        /// Authenticated close (via network)
        /// </summary>
        /// <param name="agentID"></param>
        /// <param name="force"></param>
        /// <param name="auth_token"></param>
        /// <returns></returns>
        public bool CloseAgent(UUID agentID, bool force, string auth_token)
        {
            //m_log.DebugFormat("[SCENE]: Processing incoming close agent {0} in region {1} with auth_token {2}", agentID, RegionInfo.RegionName, auth_token);

            // Check that the auth_token is valid
            AgentCircuitData acd = AuthenticateHandler.GetAgentCircuitData(agentID);

            if (acd is null)
            {
                m_log.DebugFormat(
                    "[SCENE]: Request to close agent {0} but no such agent in scene {1}.  May have been closed previously.",
                    agentID, Name);

                return false;
            }

            if (acd.SessionID.ToString() == auth_token)
            {
                return CloseAgent(agentID, force);
            }
            else
            {
                m_log.WarnFormat(
                    "[SCENE]: Request to close agent {0} with invalid authorization token {1} in {2}",
                    agentID, auth_token, Name);
            }

            return false;
        }

        /// <summary>
        /// Tell a single client to prepare to close.
        /// </summary>
        /// <remarks>
        /// This should only be called if we may close the client but there will be some delay in so doing.  Meant for
        /// internal use - other callers should almost certainly called CloseClient().
        /// </remarks>
        /// <param name="sp"></param>
        /// <returns>true if pre-close state notification was successful.  false if the agent
        /// was not in a state where it could transition to pre-close.</returns>
        public bool IncomingPreCloseClient(ScenePresence sp)
        {
            lock (m_removeClientLock)
            {
                // We need to avoid a race condition where in, for example, an A B C D region layout, an avatar may
                // teleport from A -> D, but then -> C before A has asked B to close its old child agent.  We do not
                // want to obey this close since C may have renewed the child agent lease on B.
                if (sp.DoNotCloseAfterTeleport)
                {
                    m_log.DebugFormat(
                        "[SCENE]: Not pre-closing {0} agent {1} in {2} since another simulator has re-established the child connection",
                        sp.IsChildAgent ? "child" : "root", sp.Name, Name);

                    // Need to reset the flag so that a subsequent close after another teleport can succeed.
                    sp.DoNotCloseAfterTeleport = false;

                    return false;
                }

                if (sp.LifecycleState != ScenePresenceState.Running)
                {
                    m_log.DebugFormat(
                        "[SCENE]: Called IncomingPreCloseAgent() for {0} in {1} but presence is already in state {2}",
                        sp.Name, Name, sp.LifecycleState);

                    return false;
                }

                sp.LifecycleState = ScenePresenceState.PreRemove;

                return true;
            }
        }

        /// <summary>
        /// Tell a single agent to disconnect from the region.
        /// </summary>
        /// <param name="agentID"></param>
        /// <param name="force">
        /// Force the agent to close even if it might be in the middle of some other operation.  You do not want to
        /// force unless you are absolutely sure that the agent is dead and a normal close is not working.
        /// </param>
        public override bool CloseAgent(UUID agentID, bool force)
        {
            ScenePresence sp;

            lock (m_removeClientLock)
            {
                sp = GetScenePresence(agentID);

                if (sp is null || sp.IsDeleted)
                {
                    // If there is no scene presence, we may be handling a dead
                    // client. These can keep an avatar from reentering a region
                    // and since they don't get cleaned up they will stick
                    // around until region restart. So, if there is no SP,
                    // remove the client as well.
                    bool ret = false;
                    if (m_clientManager.TryGetValue(agentID, out IClientAPI client))
                    {
                        client.Close(force, force);
                        m_log.DebugFormat( "[SCENE]: Dead client for agent ID {0} was cleaned up in {1}", agentID, Name);
                        ret = true;
                    }

                    // need to try this again, bc client close may had not done it
                    m_authenticateHandler?.RemoveCircuit(agentID);
                    m_clientManager?.Remove(agentID);
                    m_capsModule?.RemoveCaps(agentID, 0);

                    return ret;
                }

                if (sp.LifecycleState != ScenePresenceState.Running && sp.LifecycleState != ScenePresenceState.PreRemove)
                {
                    m_log.DebugFormat(
                        "[SCENE]: Called CloseClient() for {0} in {1} but presence is already in state {2}",
                        sp.Name, Name, sp.LifecycleState);

                    return false;
                }

                // We need to avoid a race condition where in, for example, an A B C D region layout, an avatar may
                // teleport from A -> D, but then -> C before A has asked B to close its old child agent.  We do not
                // want to obey this close since C may have renewed the child agent lease on B.
                if (sp.DoNotCloseAfterTeleport)
                {
                    m_log.DebugFormat(
                        "[SCENE]: Not closing {0} agent {1} in {2} since another simulator has re-established the child connection",
                        sp.IsChildAgent ? "child" : "root", sp.Name, Name);

                    // Need to reset the flag so that a subsequent close after another teleport can succeed.
                    sp.DoNotCloseAfterTeleport = false;

                    return false;
                }

                sp.LifecycleState = ScenePresenceState.Removing;
            }

            if (sp is not null)
            {
                sp.ControllingClient.Close(force, force);

                if(sp.IsNPC && UserManagementModule is not null)
                    UserManagementModule.RemoveUser(sp.UUID);

                return true;
            }

            return false;
        }


        /// <summary>
        /// Tries to teleport agent within region.
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="position"></param>
        /// <param name="lookAt"></param>
        /// <param name="teleportFlags"></param>
        public void RequestLocalTeleport(ScenePresence sp, Vector3 position, Vector3 vel,
                                            Vector3 lookat, int flags)
        {
            sp.LocalTeleport(position, vel, lookat, flags);
        }

        /// <summary>
        /// Tries to teleport agent to another region.
        /// </summary>
        /// <remarks>
        /// The region name must exactly match that given.
        /// </remarks>
        /// <param name="remoteClient"></param>
        /// <param name="regionName"></param>
        /// <param name="position"></param>
        /// <param name="lookAt"></param>
        /// <param name="teleportFlags"></param>
        public void RequestTeleportLocation(IClientAPI remoteClient, string regionName, Vector3 position,
                                            Vector3 lookat, uint teleportFlags)
        {
            if (EntityTransferModule is null)
            {
                m_log.DebugFormat("[SCENE]: Unable to perform teleports: no AgentTransferModule is active");
                return;
            }

            ScenePresence sp = GetScenePresence(remoteClient.AgentId);
            if (sp is null || sp.IsDeleted || sp.IsInTransit)
                return;

            ulong regionHandle = 0;
            if(regionName == RegionInfo.RegionName)
                regionHandle = RegionInfo.RegionHandle;
            else
            {
                GridRegion region = GridService.GetRegionByName(RegionInfo.ScopeID, regionName);
                if (region is not null)
                    regionHandle = region.RegionHandle;
            }

            if(regionHandle == 0)
            {
                // can't find the region: Tell viewer and abort
                remoteClient.SendTeleportFailed($"The region '{regionName}' could not be found.");
                return;
            }

            EntityTransferModule.Teleport(sp, regionHandle, position, lookat, teleportFlags);
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
            if (EntityTransferModule is null)
            {
                m_log.Debug("[SCENE]: Unable to perform teleports: no AgentTransferModule is active");
                return;
            }

            ScenePresence sp = GetScenePresence(remoteClient.AgentId);
            if (sp is null || sp.IsDeleted || sp.IsInTransit)
                return;

            EntityTransferModule.Teleport(sp, regionHandle, position, lookAt, teleportFlags);
        }

        public void RequestTeleportLandmark(IClientAPI remoteClient, AssetLandmark lm, Vector3 lookAt)
        {
            if (EntityTransferModule is null)
            {
                m_log.Debug("[SCENE]: Unable to perform teleports: no AgentTransferModule is active");
                return;
            }

            ScenePresence sp = GetScenePresence(remoteClient.AgentId);
            if (sp is null || sp.IsDeleted || sp.IsInTransit)
                return;
            EntityTransferModule.RequestTeleportLandmark(remoteClient, lm, lookAt);
        }

        public bool CrossAgentToNewRegion(ScenePresence agent, bool isFlying)
        {
            if(!AllowAvatarCrossing)
                return false;

            if (EntityTransferModule is not null)
            {
                return EntityTransferModule.Cross(agent, isFlying);
            }
            else
            {
                m_log.Debug("[SCENE]: Unable to cross agent to neighbouring region, because there is no AgentTransferModule");
            }

            return false;
        }

        public void SendOutChildAgentUpdates(AgentPosition cadu, ScenePresence presence)
        {
            m_sceneGridService.SendChildAgentDataUpdate(cadu, presence);
        }

        #endregion

        #region Other Methods

        protected override IConfigSource GetConfig()
        {
            return m_config;
        }

        #endregion

        public void HandleObjectPermissionsUpdate(IClientAPI controller, UUID agentID, UUID sessionID, byte field, uint localId, uint mask, byte set)
        {
            // Check for spoofing..  since this is permissions we're talking about here!
            if (controller.SessionId.Equals(sessionID) && controller.AgentId.Equals(agentID))
            {
                // Tell the object to do permission update
                if (localId != 0)
                {
                    SceneObjectGroup chObjectGroup = GetGroupByPrim(localId);
                    chObjectGroup?.UpdatePermissions(agentID, field, localId, mask, set);
                }
            }
        }

        /// <summary>
        /// Causes all clients to get a full object update on all of the objects in the scene.
        /// </summary>
        public void ForceClientUpdate()
        {
            EntityBase[] entityList = GetEntities();
            foreach (EntityBase ent in entityList)
            {
                if (ent is SceneObjectGroup sog)
                {
                    sog.ScheduleGroupForFullUpdate();
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
            m_log.Debug($"Searching for Primitive: '{cmdparams[2]}'");

            EntityBase[] entityList = GetEntities();
            foreach (EntityBase ent in entityList)
            {
                if (ent is SceneObjectGroup sog)
                {
                    SceneObjectPart part = sog.GetPart(sog.UUID);
                    if (part is not null)
                    {
                        if (part.Name == cmdparams[2])
                        {
                            part.Resize(
                                new Vector3(Convert.ToSingle(cmdparams[3]), Convert.ToSingle(cmdparams[4]),
                                              Convert.ToSingle(cmdparams[5])));

                            m_log.Debug($"Edited scale of Primitive: {part.Name}");
                        }
                    }
                }
            }
        }

        #region Script Handling Methods

        /// <summary>
        /// Console command handler to send script command to script engine.
        /// </summary>
        /// <param name="args"></param>
        public void SendCommandToPlugins(string[] args)
        {
            m_eventManager.TriggerOnPluginConsole(args);
        }

        public LandData GetLandData(float x, float y)
        {
            ILandObject parcel = LandChannel.GetLandObject(x, y);
            if (parcel is null)
                return null;
            return parcel.LandData;
        }

        /// <summary>
        /// Get LandData by position.
        /// </summary>
        /// <param name="pos"></param>
        /// <returns></returns>
        public LandData GetLandData(Vector3 pos)
        {
            return GetLandData(pos.X, pos.Y);
        }

        public LandData GetLandData(uint x, uint y)
        {
//            m_log.DebugFormat("[SCENE]: returning land for {0},{1}", x, y);
            ILandObject parcel = LandChannel.GetLandObject((int)x, (int)y);
            if (parcel is null)
                return null;
            return parcel.LandData;
        }

        #endregion

        #region Script Engine

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool LSLScriptDanger(SceneObjectPart part, Vector3 pos)
        {
            ILandObject parcel = LandChannel.GetLandObject(pos);
            return LSLScriptDanger(part, parcel);
        }

        public bool LSLScriptDanger(SceneObjectPart part, ILandObject parcel)
        {
            if (parcel is null)
                return true;
            
            LandData ldata = parcel.LandData;
            if (ldata is null)
                return true;
     
            uint landflags = ldata.Flags;
        
            uint mask = (uint)(ParcelFlags.CreateObjects | ParcelFlags.AllowAPrimitiveEntry);
            if((landflags & mask) != mask)
                return true;
 
            if((landflags & (uint)ParcelFlags.AllowOtherScripts) != 0)
                return false;

            if(part is null)
                return true;
            if(part.GroupID == ldata.GroupID && (landflags & (uint)ParcelFlags.AllowGroupScripts) != 0)
                return false;

            return true;
        }

        private bool ScriptDanger(SceneObjectPart part, Vector3 pos)
        {
            if (part is null)
                return false;

            ILandObject parcel = LandChannel.GetLandObject(pos.X, pos.Y);
            if (parcel is not null)
            {
                if ((parcel.LandData.Flags & (uint)ParcelFlags.AllowOtherScripts) != 0)
                    return true;

                if ((part.OwnerID.Equals(parcel.LandData.OwnerID)) || Permissions.IsGod(part.OwnerID))
                    return true;

                if (((parcel.LandData.Flags & (uint)ParcelFlags.AllowGroupScripts) != 0)
                    && (!parcel.LandData.GroupID.IsZero()) && (parcel.LandData.GroupID.Equals(part.GroupID)))
                    return true;
            }
            else
            {
                if (pos.X > 0f && pos.X < RegionInfo.RegionSizeX && pos.Y > 0f && pos.Y < RegionInfo.RegionSizeY)
                    return true;
            }

            return false;
        }

        public bool PipeEventsForScript(uint localID)
        {
            SceneObjectPart part = GetSceneObjectPart(localID);

            if (part is not null)
            {
                SceneObjectPart parent = part.ParentGroup.RootPart;
                return ScriptDanger(parent, parent.GetWorldPosition());
            }
            else
            {
                return false;
            }
        }

        #endregion

        #region SceneGraph wrapper methods

        public void SwapRootAgentCount(bool direction_RoottoChild, bool isnpc)
        {
            m_sceneGraph.SwapRootChildAgent(direction_RoottoChild, isnpc);
        }

        public void AddPhysicalPrim(int num)
        {
            m_sceneGraph.AddPhysicalPrim(num);
        }

        public void RemovePhysicalPrim(int num)
        {
            m_sceneGraph.RemovePhysicalPrim(num);
        }

        public int GetRootAgentCount()
        {
            return m_sceneGraph.GetRootAgentCount();
        }

        public int GetRootNPCCount()
        {
            return m_sceneGraph.GetRootAgentCount();
        }

        public int GetChildAgentCount()
        {
            return m_sceneGraph.GetChildAgentCount();
        }

        /// <summary>
        /// Request a scene presence by UUID. Fast, indexed lookup.
        /// </summary>
        /// <param name="agentID"></param>
        /// <returns>null if the presence was not found</returns>
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ScenePresence GetScenePresence(UUID agentID)
        {
            return m_sceneGraph.GetScenePresence(agentID);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool TryGetScenePresence(UUID agentID, out ScenePresence presence)
        {
            return m_sceneGraph.TryGetScenePresence(agentID, out presence);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetSceneRootPresence(UUID agentID, out ScenePresence presence)
        {
            return m_sceneGraph.TryGetSceneRootPresence(agentID, out presence);
        }
        /// <summary>
        /// Request the scene presence by name.
        /// </summary>
        /// <param name="firstName"></param>
        /// <param name="lastName"></param>
        /// <returns>null if the presence was not found</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ScenePresence GetScenePresence(string firstName, string lastName)
        {
            return m_sceneGraph.GetScenePresence(firstName, lastName);
        }

        /// <summary>
        /// Request the scene presence by localID.
        /// </summary>
        /// <param name="localID"></param>
        /// <returns>null if the presence was not found</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ScenePresence GetScenePresence(uint localID)
        {
            return m_sceneGraph.GetScenePresence(localID);
        }

        /// <summary>
        /// Gets all the scene presences in this scene.
        /// </summary>
        /// <remarks>
        /// This method will return both root and child scene presences.
        ///
        /// Consider using only on ForEachScenePresence() or ForEachRootScenePresence() if possible since these will not
        /// involving creating a new List object.
        /// </remarks>
        /// <returns>
        /// The shared list of the scene presences.
        /// </returns>
        /// WARNING DO NOT MODIFY RETURNED VALUE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public List<ScenePresence> GetScenePresences()
        {
            //return new List<ScenePresence>(m_sceneGraph.GetScenePresences());
            return m_sceneGraph.GetScenePresences();
        }

        /// <summary>
        /// Performs action on all avatars in the scene (root scene presences)
        /// Avatars may be an NPC or a 'real' client.
        /// </summary>
        /// <param name="action"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ForEachRootScenePresence(Action<ScenePresence> action)
        {
            m_sceneGraph.ForEachRootScenePresence(action);
        }

        /// <summary>
        /// Performs action on all scene presences (root and child)
        /// </summary>
        /// <param name="action"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ForEachScenePresence(Action<ScenePresence> action)
        {
            m_sceneGraph.ForEachScenePresence(action);
        }

        /// <summary>
        /// Get all the scene object groups.
        /// </summary>
        /// <returns>
        /// The scene object groups.  If the scene is empty then an empty list is returned.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public List<SceneObjectGroup> GetSceneObjectGroups()
        {
            return m_sceneGraph.GetSceneObjectGroups();
        }

        /// <summary>
        /// Get a group via its UUID
        /// </summary>
        /// <param name="fullID"></param>
        /// <returns>null if no group with that id exists</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SceneObjectGroup GetSceneObjectGroup(UUID fullID)
        {
            return m_sceneGraph.GetSceneObjectGroup(fullID);
        }

        /// <summary>
        /// Get a group via its local ID
        /// </summary>
        /// <remarks>This will only return a group if the local ID matches a root part</remarks>
        /// <param name="localID"></param>
        /// <returns>null if no group with that id exists</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SceneObjectGroup GetSceneObjectGroup(uint localID)
        {
            return m_sceneGraph.GetSceneObjectGroup(localID);
        }

        /// <summary>
        /// Get a group by name from the scene (will return the first
        /// found, if there are more than one prim with the same name)
        /// </summary>
        /// <param name="name"></param>
        /// <returns>null if no group with that name exists</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SceneObjectGroup GetSceneObjectGroup(string name)
        {
            return m_sceneGraph.GetSceneObjectGroup(name);
        }

        /// <summary>
        /// Attempt to get the SOG via its UUID
        /// </summary>
        /// <param name="fullID"></param>
        /// <param name="sog"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetSceneObjectGroup(UUID fullID, out SceneObjectGroup sog)
        {
            sog = GetSceneObjectGroup(fullID);
            return sog is not null;
        }

        /// <summary>
        /// Get a prim by name from the scene (will return the first
        /// found, if there are more than one prim with the same name)
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SceneObjectPart GetSceneObjectPart(string name)
        {
            return m_sceneGraph.GetSceneObjectPart(name);
        }

        /// <summary>
        /// Get a prim via its local id
        /// </summary>
        /// <param name="localID"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SceneObjectPart GetSceneObjectPart(uint localID)
        {
            return m_sceneGraph.GetSceneObjectPart(localID);
        }

        /// <summary>
        /// Get a prim via its UUID
        /// </summary>
        /// <param name="fullID"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SceneObjectPart GetSceneObjectPart(UUID fullID)
        {
            return m_sceneGraph.GetSceneObjectPart(fullID);
        }

        /// <summary>
        /// Attempt to get a prim via its UUID
        /// </summary>
        /// <param name="fullID"></param>
        /// <param name="sop"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetSceneObjectPart(UUID fullID, out SceneObjectPart sop)
        {
            return m_sceneGraph.TryGetSceneObjectPart(fullID, out sop);
        }

        /// <summary>
        /// Get a scene object group that contains the prim with the given local id
        /// </summary>
        /// <param name="localID"></param>
        /// <returns>null if no scene object group containing that prim is found</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SceneObjectGroup GetGroupByPrim(uint localID)
        {
            return m_sceneGraph.GetGroupByPrim(localID);
        }

        /// <summary>
        /// Get a scene object group that contains the prim with the given uuid
        /// </summary>
        /// <param name="fullID"></param>
        /// <returns>null if no scene object group containing that prim is found</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SceneObjectGroup GetGroupByPrim(UUID fullID)
        {
            return m_sceneGraph.GetGroupByPrim(fullID);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetAvatarByName(string avatarName, out ScenePresence avatar)
        {
            return m_sceneGraph.TryGetAvatarByName(avatarName, out avatar);
        }

        /// <summary>
        /// Perform an action on all clients with an avatar in this scene (root only)
        /// </summary>
        /// <param name="action"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ForEachRootClient(Action<IClientAPI> action)
        {
            ForEachRootScenePresence(delegate(ScenePresence presence)
            {
                action(presence.ControllingClient);
            });
        }

        /// <summary>
        /// Perform an action on all clients connected to the region (root and child)
        /// </summary>
        /// <param name="action"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ForEachClient(Action<IClientAPI> action)
        {
            m_clientManager.ForEach(action);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetNumberOfClients()
        {
            return m_clientManager.Count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetClient(UUID avatarID, out IClientAPI client)
        {
            return m_clientManager.TryGetValue(avatarID, out client);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetClient(System.Net.IPEndPoint remoteEndPoint, out IClientAPI client)
        {
            return m_clientManager.TryGetValue(remoteEndPoint, out client);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ForEachSOG(Action<SceneObjectGroup> action)
        {
            m_sceneGraph.ForEachSOG(action);
        }

        /// <summary>
        /// Returns a list of the entities in the scene.  This is a new list so operations perform on the list itself
        /// will not affect the original list of objects in the scene.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EntityBase[] GetEntities()
        {
            return m_sceneGraph.GetEntities();
        }

        #endregion


        // Commented pending deletion since this method no longer appears to do anything at all
        //        public bool NeedSceneCacheClear(UUID agentID)
        //        {
        //            IInventoryTransferModule inv = RequestModuleInterface<IInventoryTransferModule>();
        //            if (inv is null)
        //                return true;
        //
        //            return inv.NeedSceneCacheClear(agentID, this);
        //        }

        public void CleanTempObjects()
        {
            DateTime now =  DateTime.UtcNow;
            EntityBase[] entities = GetEntities();
            foreach (EntityBase obj in entities)
            {
                if (obj is SceneObjectGroup)
                {
                    SceneObjectGroup grp = obj as SceneObjectGroup;

                    if (!grp.IsDeleted)
                    {
                        if ((grp.RootPart.Flags & PrimFlags.TemporaryOnRez) != 0)
                        {
                            if (grp.GetSittingAvatarsCount() == 0 && grp.RootPart.Expires <= now)
                                DeleteSceneObject(grp, false);
                        }
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DeleteFromStorage(UUID uuid)
        {
            SimulationDataService.RemoveObject(uuid, RegionInfo.RegionID);
        }

        public int GetHealth(out int flags, out string message)
        {
            // Returns:
            // 1 = sim is up and accepting http requests. The heartbeat has
            // stopped and the sim is probably locked up, but a remote
            // admin restart may succeed
            //
            // 2 = Sim is up and the heartbeat is running. The sim is likely
            // usable for people within
            //
            // 3 = Sim is up and one packet thread is running. Sim is
            // unstable and will not accept new logins
            //
            // 4 = Sim is up and both packet threads are running. Sim is
            // likely usable
            //
            // 5 = We have seen a new user enter within the past 4 minutes
            // which can be seen as positive confirmation of sim health
            //
            int health = 1; // Start at 1, means we're up

            flags = 0;
            message = String.Empty;

            CheckHeartbeat();

            if (m_firstHeartbeat || (m_lastIncoming == 0 && m_lastOutgoing == 0))
            {
                // We're still starting
                // 0 means "in startup", it can't happen another way, since
                // to get here, we must be able to accept http connections
                return 0;
            }

            if ((Util.EnvironmentTickCountSubtract(m_lastFrameTick)) < 4000)
            {
                health+=1;
                flags |= 1;
            }

            if (Util.EnvironmentTickCountSubtract(m_lastIncoming) < 6000)
            {
                health+=1;
                flags |= 2;
            }

            if (Util.EnvironmentTickCountSubtract(m_lastOutgoing) < 6000)
            {
                health+=1;
                flags |= 4;
            }
            /*
            else
            {
int pid = System.Diagnostics.Process.GetCurrentProcess().Id;
System.Diagnostics.Process proc = new System.Diagnostics.Process();
proc.EnableRaisingEvents=false;
proc.StartInfo.FileName = "/bin/kill";
proc.StartInfo.Arguments = "-QUIT " + pid.ToString();
proc.Start();
proc.WaitForExit();
Thread.Sleep(1000);
Environment.Exit(1);
            }
            */

            if (flags != 7)
                return health;

            // A login in the last 4 mins? We can't be doing too badly
            //
            if (Util.EnvironmentTickCountSubtract(m_LastLogin) < 240000)
                health++;
            else
                return health;

            return health;
        }

        public Scene ConsoleScene()
        {
            if (MainConsole.Instance?.ConsoleScene is Scene sc)
                return sc;
            return null;
        }

        // Get terrain height at the specified <x,y> location.
        // Presumes the underlying implementation is a heightmap which is a 1m grid.

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float GetGroundHeight(float x, float y)
        {
           return Heightmap.GetHeight(x, y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CheckHeartbeat()
        {
            if (m_firstHeartbeat)
                return;

            if ((Util.EnvironmentTickCountSubtract(m_lastFrameTick)) > 5000)
                Start();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override ISceneObject DeserializeObject(string representation)
        {
            return SceneObjectSerializer.FromXml2Format(representation);
        }

        public override bool AllowScriptCrossings
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return m_allowScriptCrossings; }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3 GetNearestAllowedPosition(ScenePresence avatar)
        {
            return GetNearestAllowedPosition(avatar, null);
        }

        public Vector3 GetNearestAllowedPosition(ScenePresence avatar, ILandObject excludeParcel)
        {
            Vector3 pos = avatar.AbsolutePosition;

            ILandObject nearestParcel = GetNearestAllowedParcel(avatar.UUID, pos.X, pos.Y, excludeParcel);

            if (nearestParcel is not null)
            {
                Vector2? nearestPoint = null;
                Vector3 dir = -avatar.Velocity;
                float dirlen = dir.Length();
                if(dirlen > 1.0f)
                    //Try to get a location that feels like where they came from
                    nearestPoint = nearestParcel.GetNearestPointAlongDirection(pos, dir);

                nearestPoint ??= nearestParcel.GetNearestPoint(pos);

                if (nearestPoint is not null)
                {
                    return GetPositionAtAvatarHeightOrGroundHeight(avatar,
                            nearestPoint.Value.X, nearestPoint.Value.Y);
                }

                ILandObject dest = LandChannel.GetLandObject(avatar.lastKnownAllowedPosition.X, avatar.lastKnownAllowedPosition.Y);
                if (dest != excludeParcel)
                {
                    // Ultimate backup if we have no idea where they are and
                    // the last allowed position was in another parcel
                    m_log.Debug("Have no idea where they are, sending them to: " + avatar.lastKnownAllowedPosition.ToString());
                    return avatar.lastKnownAllowedPosition;
                }

                // else fall through to region edge
            }

            //Go to the edge, this happens in teleporting to a region with no available parcels
            Vector3 nearestRegionEdgePoint = GetNearestRegionEdgePosition(avatar);

            //Debug.WriteLine("They are really in a place they don't belong, sending them to: " + nearestRegionEdgePoint.ToString());
            return nearestRegionEdgePoint;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Vector3 GetParcelCenterAtGround(ILandObject parcel)
        {
            Vector2 center = parcel.CenterPoint;
            return GetPositionAtGround(center.X, center.Y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ILandObject GetNearestAllowedParcel(UUID avatarId, float x, float y)
        {
            return GetNearestAllowedParcel(avatarId, x, y, null);
        }

        public ILandObject GetNearestAllowedParcel(UUID avatarId, float x, float y, ILandObject excludeParcel)
        {
            if(LandChannel is null)
                return null;

            List<ILandObject> all = LandChannel.AllParcels();

            if(all is null || all.Count == 0)
                return null;

            float minParcelDistanceSQ = float.MaxValue;
            ILandObject nearestParcel = null;
            Vector2 curCenter;
            float parcelDistanceSQ;

            foreach (var parcel in all)
            {
                if (parcel != excludeParcel && !parcel.IsEitherBannedOrRestricted(avatarId))
                {
                    curCenter = parcel.CenterPoint;
                    curCenter.X -= x;
                    curCenter.Y -= y;
                    parcelDistanceSQ = curCenter.LengthSquared();
                    if (parcelDistanceSQ < minParcelDistanceSQ)
                    {
                        minParcelDistanceSQ = parcelDistanceSQ;
                        nearestParcel = parcel;
                    }
                }
            }

            return nearestParcel;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector2 GetParcelSafeCorner(ILandObject parcel)
        {
            Vector2 place = parcel.StartPoint;
            place.X += 2f;
            place.Y += 2f;
            return place;
        }

        private Vector3 GetNearestRegionEdgePosition(ScenePresence avatar)
        {
            float posX = avatar.AbsolutePosition.X;
            float posY = avatar.AbsolutePosition.Y;
            float regionSizeX = RegionInfo.RegionSizeX;
            float halfRegionSizeX = regionSizeX * 0.5f;
            float regionSizeY = RegionInfo.RegionSizeY;
            float halfRegionSizeY = regionSizeY * 0.5f;

            float xdistance = posX < halfRegionSizeX ? posX : regionSizeX - posX;
            float ydistance = posY < halfRegionSizeY ? posY : regionSizeY - posY;

            //find out what vertical edge to go to
            if (xdistance < ydistance)
            {
                if (posX < halfRegionSizeX)
                    return GetPositionAtAvatarHeightOrGroundHeight(avatar, 0.5f, posY);
                else
                    return GetPositionAtAvatarHeightOrGroundHeight(avatar, regionSizeX - 0.5f, posY);
            }
            //find out what horizontal edge to go to
            else
            {
                if (posY < halfRegionSizeY)
                    return GetPositionAtAvatarHeightOrGroundHeight(avatar, posX, 0.5f);
                else
                    return GetPositionAtAvatarHeightOrGroundHeight(avatar, posX, regionSizeY - 0.5f);
            }
        }

        private Vector3 GetPositionAtAvatarHeightOrGroundHeight(ScenePresence avatar, float x, float y)
        {
            Vector3 ground = GetPositionAtGround(x, y);
            if(avatar.Appearance is not null)
                ground.Z += avatar.Appearance.AvatarHeight * 0.5f;
            else
                ground.Z += 0.8f;

            if (avatar.AbsolutePosition.Z > ground.Z)
            {
                ground.Z = avatar.AbsolutePosition.Z;
            }
            return ground;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Vector3 GetPositionAtGround(float x, float y)
        {
            return new Vector3(x, y, GetGroundHeight(x, y));
        }

        public List<UUID> GetEstateRegions(int estateID)
        {
            IEstateDataService estateDataService = EstateDataService;
            if (estateDataService is null)
                return new List<UUID>(0);

            return estateDataService.GetRegions(estateID);
        }

        public void ReloadEstateData()
        {
            IEstateDataService estateDataService = EstateDataService;
            if (estateDataService is not null)
            {
                bool parcelEnvOvr = RegionInfo.EstateSettings.AllowEnvironmentOverride;
                EstateSettings es = estateDataService.LoadEstateSettings(RegionInfo.RegionID, false);
                if (es == null)
                    m_log.Error($"[SCENE]: Region {RegionInfo.RegionName} failed to reload estate settings. Using defaults");
                RegionInfo.EstateSettings = es;
                if(parcelEnvOvr && !RegionInfo.EstateSettings.AllowEnvironmentOverride)
                    ClearAllParcelEnvironments();
            }
        }

        public void ClearAllParcelEnvironments()
        {
            IEnvironmentModule envM = RequestModuleInterface<IEnvironmentModule>();
            if(LandChannel is not null && envM is not null)
            {
                LandChannel.ClearAllEnvironments();
                envM.WindlightRefresh(1,false);
            }
        }
        private void HandleReloadEstate(string module, string[] cmd)
        {
            if (MainConsole.Instance.ConsoleScene is null ||
                (MainConsole.Instance.ConsoleScene is Scene sc &&
                sc == this))
            {
                ReloadEstateData();
            }
        }

        /// <summary>
        /// Get the volume of space that will encompass all the given objects.
        /// </summary>
        /// <param name="objects"></param>
        /// <param name="minX"></param>
        /// <param name="maxX"></param>
        /// <param name="minY"></param>
        /// <param name="maxY"></param>
        /// <param name="minZ"></param>
        /// <param name="maxZ"></param>
        /// <returns></returns>
        public static Vector3[] GetCombinedBoundingBox(
           List<SceneObjectGroup> objects,
           out float minX, out float maxX, out float minY, out float maxY, out float minZ, out float maxZ)
        {
            minX = float.MaxValue;
            maxX = float.MinValue;
            minY = float.MaxValue;
            maxY = float.MinValue;
            minZ = float.MaxValue;
            maxZ = float.MinValue;

            List<Vector3> offsets = new();

            foreach (SceneObjectGroup g in objects)
            {
                Vector3 vec = g.AbsolutePosition;

                g.GetAxisAlignedBoundingBoxRaw(out float ominX, out float omaxX, out float ominY,
                    out float omaxY, out float ominZ, out float omaxZ);

//                m_log.DebugFormat(
//                    "[SCENE]: For {0} found AxisAlignedBoundingBoxRaw {1}, {2}",
//                    g.Name, new Vector3(ominX, ominY, ominZ), new Vector3(omaxX, omaxY, omaxZ));

                ominX += vec.X;
                omaxX += vec.X;
                ominY += vec.Y;
                omaxY += vec.Y;
                ominZ += vec.Z;
                omaxZ += vec.Z;

                if (minX > ominX)
                    minX = ominX;
                if (minY > ominY)
                    minY = ominY;
                if (minZ > ominZ)
                    minZ = ominZ;
                if (maxX < omaxX)
                    maxX = omaxX;
                if (maxY < omaxY)
                    maxY = omaxY;
                if (maxZ < omaxZ)
                    maxZ = omaxZ;
            }

            foreach (SceneObjectGroup g in objects)
            {
                Vector3 vec = g.AbsolutePosition;
                vec.X -= minX;
                vec.Y -= minY;
                vec.Z -= minZ;

                offsets.Add(vec);
            }

            return offsets.ToArray();
        }

        /// <summary>
        /// Regenerate the maptile for this scene.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RegenerateMaptile()
        {
            IWorldMapModule mapModule = RequestModuleInterface<IWorldMapModule>();
            mapModule?.GenerateMaptile();
        }

        //        public void CleanDroppedAttachments()
        //        {
        //            List<SceneObjectGroup> objectsToDelete =
        //                    new List<SceneObjectGroup>();
        //
        //            lock (m_cleaningAttachments)
        //            {
        //                ForEachSOG(delegate (SceneObjectGroup grp)
        //                        {
        //                            if (grp.RootPart.Shape.PCode == 0 && grp.RootPart.Shape.State != 0 && (!objectsToDelete.Contains(grp)))
        //                            {
        //                                UUID agentID = grp.OwnerID;
        //                                if (agentID.IsZero())
        //                                {
        //                                    objectsToDelete.Add(grp);
        //                                    return;
        //                                }
        //
        //                                ScenePresence sp = GetScenePresence(agentID);
        //                                if (sp is null)
        //                                {
        //                                    objectsToDelete.Add(grp);
        //                                    return;
        //                                }
        //                            }
        //                        });
        //            }
        //
        //            foreach (SceneObjectGroup grp in objectsToDelete)
        //            {
        //                m_log.InfoFormat("[SCENE]: Deleting dropped attachment {0} of user {1}", grp.UUID, grp.OwnerID);
        //                DeleteSceneObject(grp, true);
        //            }
        //        }

        public void ThreadAlive(int threadCode)
        {
            switch(threadCode)
            {
                case 1: // Incoming
                    m_lastIncoming = Util.EnvironmentTickCount();
                    break;
                case 2: // Outgoing
                    m_lastOutgoing = Util.EnvironmentTickCount();
                    break;
            }
        }

        public void RegenerateMaptileAndReregister(object sender, ElapsedEventArgs e)
        {
            RegenerateMaptile();

            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.Default;

            // We need to propagate the new image UUID to the grid service
            // so that all simulators can retrieve it
            string error = GridService.RegisterRegion(RegionInfo.ScopeID, new GridRegion(RegionInfo));
            if (error != string.Empty)
                throw new Exception(error);
            if(m_generateMaptiles)
                m_mapGenerationTimer.Start();
        }

        /// <summary>
        /// This method is called across the simulation connector to
        /// determine if a given agent is allowed in this region
        /// AS A ROOT AGENT
        /// </summary>
        /// <remarks>
        /// Returning false here will prevent them
        /// from logging into the region, teleporting into the region
        /// or corssing the broder walking, but will NOT prevent
        /// child agent creation, thereby emulating the SL behavior.
        /// </remarks>
        /// <param name='agentID'>The visitor's User ID</param>
        /// <param name="agentHomeURI">The visitor's Home URI (may be null)</param>
        /// <param name='position'></param>
        /// <param name='reason'></param>
        /// <returns></returns>
        public bool QueryAccess(UUID agentID, string agentHomeURI, bool viaTeleport, Vector3 position, List<UUID> features, out string reason)
        {
            reason = string.Empty;

            if (Permissions.IsGod(agentID))
                return true;

            if (!AllowAvatarCrossing && !viaTeleport)
            {
                reason = "Region Crossing not allowed";
                return false;
            }

            AgentCircuitData aCircuit = m_authenticateHandler.GetAgentCircuitData(agentID);
            // Fake AgentCircuitData to keep IAuthorizationModule smiling
            aCircuit ??= new AgentCircuitData()
                {
                    AgentID = agentID,
                    firstname = string.Empty,
                    lastname = string.Empty
                };

            try
            {
                if (!AuthorizeUser(aCircuit, false, out reason))
                {
                    //m_log.DebugFormat("[SCENE]: Denying access for {0}", agentID);
//                    reason = "Region authorization fail";
                    return false;
                }
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[SCENE]: Exception authorizing agent: {0} " + e.StackTrace, e.Message);
                reason = "Error authorizing agent: " + e.Message;
                return false;
            }

            // last check aditional land access restrictions and relocations
            // if crossing (viaTeleport false) check only the specified parcel
            return CheckLandPositionAccess(agentID, viaTeleport, true, true, position, out reason);
        }

        // check access to land.
        public bool CheckLandPositionAccess(UUID agentID, bool NotCrossing, bool checkTeleHub, bool checkRegionAgentCount, Vector3 position, out string reason)
        {
            reason = string.Empty;

            if (Permissions.IsGod(agentID))
                return true;

            // Permissions.IsAdministrator is the same as IsGod for now
            //bool isAdmin = Permissions.IsAdministrator(agentID);
            //if(isAdmin)
            //   return true;

            // also honor estate managers access rights
            bool isManager = Permissions.IsEstateManager(agentID);
            if(isManager)
                return true;

            // FIXME: Root agent count is currently known to be inaccurate.  This forces a recount before we check.
            // However, the long term fix is to make sure root agent count is always accurate.
            m_sceneGraph.RecalculateStats();
            int num = m_sceneGraph.GetRootAgentCount();
            num -= m_sceneGraph.GetRootNPCCount();

            if (num >= RegionInfo.RegionSettings.AgentLimit)
            {
                reason = "The region is full";

                m_log.DebugFormat(
                    "[SCENE]: Denying presence with id {0} entry into {1} since region is at agent limit of {2}",
                    agentID, RegionInfo.RegionName, RegionInfo.RegionSettings.AgentLimit);

                return false;
            }

            if (NotCrossing)
            {
                if (!RegionInfo.EstateSettings.AllowDirectTeleport)
                {
                    SceneObjectGroup telehub;
                    if (!RegionInfo.RegionSettings.TelehubObject.IsZero() && (telehub = GetSceneObjectGroup  (RegionInfo.RegionSettings.TelehubObject)) is not null && checkTeleHub)
                    {
                        bool banned = true;
                        bool validTelehub = false;
                        List<SpawnPoint> spawnPoints = RegionInfo.RegionSettings.SpawnPoints();
                        Vector3 spawnPoint;
                        ILandObject land;
                        Vector3 telehubPosition = telehub.AbsolutePosition;

                        if(spawnPoints.Count == 0)
                        {
                            // will this ever happen?
                            // if so use the telehub object position
                            spawnPoint = telehubPosition;
                            land = LandChannel.GetLandObject(spawnPoint.X, spawnPoint.Y);
                            if(land is not null && !land.IsEitherBannedOrRestricted(agentID))
                            {
                                banned = false;
                                validTelehub = true;
                            }
                        }
                        else
                        {
                            Quaternion telehubRotation = telehub.GroupRotation;
                            foreach (SpawnPoint spawn in spawnPoints)
                            {
                                spawnPoint = spawn.GetLocation(telehubPosition, telehubRotation);
                                land = LandChannel.GetLandObject(spawnPoint.X, spawnPoint.Y);
                                if (land is null)
                                    continue;
                                validTelehub = true;
                                if (!land.IsEitherBannedOrRestricted(agentID))
                                {
                                    banned = false;
                                    break;
                                }
                            }
                        }

                        if(validTelehub)
                        {
                            if (banned)
                            {
                                reason = "No suitable landing point found";
                                return false;
                            }
                            else
                                return true;
                        }
                       // possible broken telehub, fall into normal check
                    }
                }

                float posX = position.X;
                float posY = position.Y;

                // allow position relocation
                if (!TestLandRestrictions(agentID, out reason, ref posX, ref posY))
                {
                    // m_log.DebugFormat("[SCENE]: Denying {0} because they are banned on all parcels", agentID);
                    reason = "You dont have access to the region parcels";
                    return false;
                }
            }
            else // check for query region crossing only
            {
                // no relocation allowed on crossings
                ILandObject land = LandChannel.GetLandObject(position.X, position.Y);
                if (land is null)
                {
                    reason = "No parcel found";
                    return false;
                }

                bool banned = land.IsBannedFromLand(agentID);
                bool restricted = land.IsRestrictedFromLand(agentID);

                if (banned || restricted)
                {
                    if (banned)
                        reason = "You are banned from the parcel";
                    else
                        reason = "The parcel is restricted";
                    return false;
                }
            }

            return true;
        }

        public void StartTimerWatchdog()
        {
            m_timerWatchdog.Interval = 1000;
            m_timerWatchdog.Elapsed += TimerWatchdog;
            m_timerWatchdog.AutoReset = true;
            m_timerWatchdog.Start();
        }

        public void TimerWatchdog(object sender, ElapsedEventArgs e)
        {
            CheckHeartbeat();

            IEtcdModule etcd = RequestModuleInterface<IEtcdModule>();
            if (etcd is not null)
            {
                int health = GetHealth(out int flags, out string _);
                if (health != m_lastHealth)
                {
                    m_lastHealth = health;

                    etcd.Store("Health", health.ToString(), 300000);
                    etcd.Store("HealthFlags", flags.ToString(), 300000);
                }

                int roots = 0;
                foreach (ScenePresence sp in GetScenePresences())
                    if (!sp.IsChildAgent && !sp.IsNPC)
                        roots++;

                if (m_lastUsers != roots)
                {
                    m_lastUsers = roots;
                    etcd.Store("RootAgents", roots.ToString(), 300000);
                }
            }
        }

        // manage and select spawn points in sequence
        public int SpawnPoint()
        {
            int spawnpoints = RegionInfo.RegionSettings.SpawnPoints().Count;

            if (spawnpoints == 0)
                return 0;

            m_SpawnPoint++;
            if (m_SpawnPoint > spawnpoints)
                m_SpawnPoint = 1;
            return m_SpawnPoint - 1;
        }

        private static void HandleGcCollect(string module, string[] args)
        {
            GC.Collect();
        }

        /// <summary>
        /// Wrappers to get physics modules retrieve assets.
        /// </summary>
        /// <remarks>
        /// Has to be done this way
        /// because we can't assign the asset service to physics directly - at the
        /// time physics are instantiated it's not registered but it will be by
        /// the time the first prim exists.
        /// </remarks>
        /// <param name="assetID"></param>
        /// <param name="callback"></param>
        public void PhysicsRequestAsset(UUID assetID, AssetReceivedDelegate callback)
        {
            AssetService.Get(assetID.ToString(), callback, PhysicsAssetReceived);
        }

        private void PhysicsAssetReceived(string id, Object sender, AssetBase asset)
        {
            AssetReceivedDelegate callback = (AssetReceivedDelegate)sender;

            callback(asset);
        }

        public string GetExtraSetting(string name)
        {
            if (m_extraSettings is not null && m_extraSettings.TryGetValue(name, out string val))
                return val;

            return String.Empty;
        }

        public void StoreExtraSetting(string name, string val)
        {
            if (m_extraSettings is null)
                return;

            if (m_extraSettings.TryGetValue(name, out string oldVal))
            {
                if (oldVal == val)
                    return;
            }

            m_extraSettings[name] = val;

            m_SimulationDataService.SaveExtra(RegionInfo.RegionID, name, val);

            m_eventManager.TriggerExtraSettingChanged(this, name, val);
        }

        public void RemoveExtraSetting(string name)
        {
            if (m_extraSettings is null)
                return;

            if (!m_extraSettings.ContainsKey(name))
                return;

            m_extraSettings.Remove(name);

            m_SimulationDataService.RemoveExtra(RegionInfo.RegionID, name);

            m_eventManager.TriggerExtraSettingChanged(this, name, String.Empty);
        }

        public bool InTeleportTargetsCoolDown(UUID sourceID, UUID targetID, int timeout)
        {
            if(TeleportTargetsCoolDown.TryGetValue(targetID, timeout, out UUID lastSource))
                return lastSource == sourceID;
            return false;
        }


    }
}
