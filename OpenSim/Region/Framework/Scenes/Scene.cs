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
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Threading;
using System.Timers;
using System.Xml;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.Packets;
using OpenMetaverse.Imaging;
using OpenSim.Framework;
using OpenSim.Framework.Monitoring;
using OpenSim.Services.Interfaces;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Console;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes.Scripting;
using OpenSim.Region.Framework.Scenes.Serialization;
using OpenSim.Region.Physics.Manager;
using Timer=System.Timers.Timer;
using TPFlags = OpenSim.Framework.Constants.TeleportFlags;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;

namespace OpenSim.Region.Framework.Scenes
{
    public delegate bool FilterAvatarList(ScenePresence avatar);

    public partial class Scene : SceneBase
    {
        private const long DEFAULT_MIN_TIME_FOR_PERSISTENCE = 60L;
        private const long DEFAULT_MAX_TIME_FOR_PERSISTENCE = 600L;

        public delegate void SynchronizeSceneHandler(Scene scene);

        #region Fields

        public bool EmergencyMonitoring = false;

        /// <summary>
        /// Show debug information about teleports.
        /// </summary>
        public bool DebugTeleporting { get; private set; }

        /// <summary>
        /// Show debug information about the scene loop.
        /// </summary>
        public bool DebugUpdates { get; private set; }

        public SynchronizeSceneHandler SynchronizeScene;

        /// <summary>
        /// Statistical information for this scene.
        /// </summary>
        public SimStatsReporter StatsReporter { get; private set; }

        public List<Border> NorthBorders = new List<Border>();
        public List<Border> EastBorders = new List<Border>();
        public List<Border> SouthBorders = new List<Border>();
        public List<Border> WestBorders = new List<Border>();

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
        public bool m_allowScriptCrossings;
        public bool m_useFlySlow;
        public bool m_useTrashOnDelete = true;

        /// <summary>
        /// Temporarily setting to trigger appearance resends at 60 second intervals.
        /// </summary>
        public bool SendPeriodicAppearanceUpdates { get; set; }

        protected float m_defaultDrawDistance = 255.0f;
        public float DefaultDrawDistance 
        {
            get { return m_defaultDrawDistance; }
        }

        private List<string> m_AllowedViewers = new List<string>();
        private List<string> m_BannedViewers = new List<string>();
        
        // TODO: need to figure out how allow client agents but deny
        // root agents when ACL denies access to root agent
        public bool m_strictAccessControl = true;
        public int MaxUndoCount = 5;

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
        protected Timer m_restartWaitTimer = new Timer();
        protected List<RegionInfo> m_regionRestartNotifyList = new List<RegionInfo>();
        protected List<RegionInfo> m_neighbours = new List<RegionInfo>();
        protected string m_simulatorVersion = "OpenSimulator Server";
        protected ModuleLoader m_moduleLoader;
        protected AgentCircuitManager m_authenticateHandler;
        protected SceneCommunicationService m_sceneGridService;

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

        protected IXMLRPC m_xmlrpcModule;
        protected IWorldComm m_worldCommModule;
        protected IAvatarFactoryModule m_AvatarFactory;
        protected IConfigSource m_config;
        protected IRegionSerialiserModule m_serialiser;
        protected IDialogModule m_dialogModule;
        protected ICapabilitiesModule m_capsModule;
        protected IGroupsModule m_groupsModule;

        private Dictionary<string, string> m_extraSettings;

        /// <summary>
        /// Current scene frame number
        /// </summary>
        public uint Frame
        {
            get;
            protected set;
        }

        /// <summary>
        /// Current maintenance run number
        /// </summary>
        public uint MaintenanceRun { get; private set; }

        /// <summary>
        /// The minimum length of time in seconds that will be taken for a scene frame.  If the frame takes less time then we
        /// will sleep for the remaining period.
        /// </summary>
        /// <remarks>
        /// One can tweak this number to experiment.  One current effect of reducing it is to make avatar animations
        /// occur too quickly (viewer 1) or with even more slide (viewer 2).
        /// </remarks>
        public float MinFrameTime { get; private set; }

        /// <summary>
        /// The minimum length of time in seconds that will be taken for a maintenance run.
        /// </summary>
        public float MinMaintenanceTime { get; private set; }

        private int m_update_physics = 1;
        private int m_update_entitymovement = 1;
        private int m_update_objects = 1;
        private int m_update_temp_cleaning = 1000;
        private int m_update_presences = 1; // Update scene presence movements
        private int m_update_events = 1;
        private int m_update_backup = 200;
        private int m_update_terrain = 50;
//        private int m_update_land = 1;
        private int m_update_coarse_locations = 50;

        private int agentMS;
        private int frameMS;
        private int physicsMS2;
        private int physicsMS;
        private int otherMS;
        private int tempOnRezMS;
        private int eventMS;
        private int backupMS;
        private int terrainMS;
        private int landMS;
        private int spareMS;

        /// <summary>
        /// Tick at which the last frame was processed.
        /// </summary>
        private int m_lastFrameTick;

        /// <summary>
        /// Tick at which the last maintenance run occurred.
        /// </summary>
        private int m_lastMaintenanceTick;

        /// <summary>
        /// Signals whether temporary objects are currently being cleaned up.  Needed because this is launched
        /// asynchronously from the update loop.
        /// </summary>
        private bool m_cleaningTemps = false;

//        private Object m_heartbeatLock = new Object();

        // TODO: Possibly stop other classes being able to manipulate this directly.
        private SceneGraph m_sceneGraph;
        private volatile int m_bordersLocked;
        private readonly Timer m_restartTimer = new Timer(15000); // Wait before firing
        private volatile bool m_backingup;
        private Dictionary<UUID, ReturnInfo> m_returns = new Dictionary<UUID, ReturnInfo>();
        private Dictionary<UUID, SceneObjectGroup> m_groupsWithTargets = new Dictionary<UUID, SceneObjectGroup>();

        private bool m_physics_enabled = true;
        private bool m_scripts_enabled = true;
        private string m_defaultScriptEngine;

        /// <summary>
        /// Tick at which the last login occurred.
        /// </summary>
        private int m_LastLogin;

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

//        private int m_lastUpdate;
//        private bool m_firstHeartbeat = true;
        
        private UpdatePrioritizationSchemes m_priorityScheme = UpdatePrioritizationSchemes.Time;
        private bool m_reprioritizationEnabled = true;
        private double m_reprioritizationInterval = 5000.0;
        private double m_rootReprioritizationDistance = 10.0;
        private double m_childReprioritizationDistance = 20.0;

        private Timer m_mapGenerationTimer = new Timer();
        private bool m_generateMaptiles;
        private bool m_useBackup = true;

        #endregion Fields

        #region Properties

        /* Used by the loadbalancer plugin on GForge */
        public int SplitRegionID
        {
            get { return m_splitRegionID; }
            set { m_splitRegionID = value; }
        }

        public bool BordersLocked
        {
            get { return m_bordersLocked == 1; }
            set
            {
                if (value == true)
                    m_bordersLocked = 1;
                else
                    m_bordersLocked = 0;
            }
        }
        
        public new float TimeDilation
        {
            get { return m_sceneGraph.PhysicsScene.TimeDilation; }
        }

        public SceneCommunicationService SceneGridService
        {
            get { return m_sceneGridService; }
        }

        public ISimulationDataService SimulationDataService
        {
            get
            {
                if (m_SimulationDataService == null)
                {
                    m_SimulationDataService = RequestModuleInterface<ISimulationDataService>();

                    if (m_SimulationDataService == null)
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
                if (m_EstateDataService == null)
                {
                    m_EstateDataService = RequestModuleInterface<IEstateDataService>();

                    if (m_EstateDataService == null)
                    {
                        throw new Exception("No IEstateDataService available.");
                    }
                }

                return m_EstateDataService;
            }
        }

        public IAssetService AssetService
        {
            get
            {
                if (m_AssetService == null)
                {
                    m_AssetService = RequestModuleInterface<IAssetService>();

                    if (m_AssetService == null)
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
                if (m_AuthorizationService == null)
                {
                    m_AuthorizationService = RequestModuleInterface<IAuthorizationService>();

                    //if (m_AuthorizationService == null)
                    //{
                    //    // don't throw an exception if no authorization service is set for the time being
                    //     m_log.InfoFormat("[SCENE]: No Authorization service is configured");
                    //}
                }

                return m_AuthorizationService;
            }
        }

        public IInventoryService InventoryService
        {
            get
            {
                if (m_InventoryService == null)
                {
                    m_InventoryService = RequestModuleInterface<IInventoryService>();

                    if (m_InventoryService == null)
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
                if (m_GridService == null)
                {
                    m_GridService = RequestModuleInterface<IGridService>();

                    if (m_GridService == null)
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
                if (m_LibraryService == null)
                    m_LibraryService = RequestModuleInterface<ILibraryService>();

                return m_LibraryService;
            }
        }

        public ISimulationService SimulationService
        {
            get
            {
                if (m_simulationService == null)
                    m_simulationService = RequestModuleInterface<ISimulationService>();

                return m_simulationService;
            }
        }

        public IAuthenticationService AuthenticationService
        {
            get
            {
                if (m_AuthenticationService == null)
                    m_AuthenticationService = RequestModuleInterface<IAuthenticationService>();
                return m_AuthenticationService;
            }
        }

        public IPresenceService PresenceService
        {
            get
            {
                if (m_PresenceService == null)
                    m_PresenceService = RequestModuleInterface<IPresenceService>();
                return m_PresenceService;
            }
        }

        public IUserAccountService UserAccountService
        {
            get
            {
                if (m_UserAccountService == null)
                    m_UserAccountService = RequestModuleInterface<IUserAccountService>();
                return m_UserAccountService;
            }
        }

        public IAvatarService AvatarService
        {
            get
            {
                if (m_AvatarService == null)
                    m_AvatarService = RequestModuleInterface<IAvatarService>();
                return m_AvatarService;
            }
        }

        public IGridUserService GridUserService
        {
            get
            {
                if (m_GridUserService == null)
                    m_GridUserService = RequestModuleInterface<IGridUserService>();
                return m_GridUserService;
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

        public int MonitorFrameTime { get { return frameMS; } }
        public int MonitorPhysicsUpdateTime { get { return physicsMS; } }
        public int MonitorPhysicsSyncTime { get { return physicsMS2; } }
        public int MonitorOtherTime { get { return otherMS; } }
        public int MonitorTempOnRezTime { get { return tempOnRezMS; } }
        public int MonitorEventTime { get { return eventMS; } } // This may need to be divided into each event?
        public int MonitorBackupTime { get { return backupMS; } }
        public int MonitorTerrainTime { get { return terrainMS; } }
        public int MonitorLandTime { get { return landMS; } }
        public int MonitorLastFrameTick { get { return m_lastFrameTick; } }

        public UpdatePrioritizationSchemes UpdatePrioritizationScheme { get { return m_priorityScheme; } }
        public bool IsReprioritizationEnabled { get { return m_reprioritizationEnabled; } }
        public double ReprioritizationInterval { get { return m_reprioritizationInterval; } }
        public double RootReprioritizationDistance { get { return m_rootReprioritizationDistance; } }
        public double ChildReprioritizationDistance { get { return m_childReprioritizationDistance; } }

        public AgentCircuitManager AuthenticateHandler
        {
            get { return m_authenticateHandler; }
        }

        public bool UseBackup
        {
            get { return m_useBackup; }
        }

        // an instance to the physics plugin's Scene object.
        public PhysicsScene PhysicsScene
        {
            get { return m_sceneGraph.PhysicsScene; }
            set
            {
                // If we're not doing the initial set
                // Then we've got to remove the previous
                // event handler
                if (PhysicsScene != null && PhysicsScene.SupportsNINJAJoints)
                {
                    PhysicsScene.OnJointMoved -= jointMoved;
                    PhysicsScene.OnJointDeactivated -= jointDeactivated;
                    PhysicsScene.OnJointErrorMessage -= jointErrorMessage;
                }

                m_sceneGraph.PhysicsScene = value;

                if (PhysicsScene != null && m_sceneGraph.PhysicsScene.SupportsNINJAJoints)
                {
                    // register event handlers to respond to joint movement/deactivation
                    PhysicsScene.OnJointMoved += jointMoved;
                    PhysicsScene.OnJointDeactivated += jointDeactivated;
                    PhysicsScene.OnJointErrorMessage += jointErrorMessage;
                }
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
            get; private set;
        }
        // allow landmarks to pass
        public bool TelehubAllowLandmarks
        {
            get; private set;
        }

        #endregion Properties

        #region Constructors

        public Scene(RegionInfo regInfo, AgentCircuitManager authen,
                     SceneCommunicationService sceneGridService,
                     ISimulationDataService simDataService, IEstateDataService estateDataService,
                     ModuleLoader moduleLoader, bool dumpAssetsToFile,
                     IConfigSource config, string simulatorVersion)
            : this(regInfo)
        {
            m_config = config;
            MinFrameTime = 0.089f;
            MinMaintenanceTime = 1;

            Random random = new Random();

            m_lastAllocatedLocalId = (uint)(random.NextDouble() * (double)(uint.MaxValue / 2)) + (uint)(uint.MaxValue / 4);
            m_moduleLoader = moduleLoader;
            m_authenticateHandler = authen;
            m_sceneGridService = sceneGridService;
            m_SimulationDataService = simDataService;
            m_EstateDataService = estateDataService;
            m_regionHandle = RegionInfo.RegionHandle;

            m_asyncSceneObjectDeleter = new AsyncSceneObjectGroupDeleter(this);
            m_asyncSceneObjectDeleter.Enabled = true;

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
            if (rs.TerrainTexture1 == UUID.Zero)
            {
                rs.TerrainTexture1 = RegionSettings.DEFAULT_TERRAIN_TEXTURE_1;
                updatedTerrainTextures = true;
            }

            if (rs.TerrainTexture2 == UUID.Zero)
            {
                rs.TerrainTexture2 = RegionSettings.DEFAULT_TERRAIN_TEXTURE_2;
                updatedTerrainTextures = true;
            }

            if (rs.TerrainTexture3 == UUID.Zero)
            {
                rs.TerrainTexture3 = RegionSettings.DEFAULT_TERRAIN_TEXTURE_3;
                updatedTerrainTextures = true;
            }

            if (rs.TerrainTexture4 == UUID.Zero)
            {
                rs.TerrainTexture4 = RegionSettings.DEFAULT_TERRAIN_TEXTURE_4;
                updatedTerrainTextures = true;
            }

            if (updatedTerrainTextures)
                rs.Save();

            RegionInfo.RegionSettings = rs;

            if (estateDataService != null)
                RegionInfo.EstateSettings = estateDataService.LoadEstateSettings(RegionInfo.RegionID, false);

            #endregion Region Settings

            //Bind Storage Manager functions to some land manager functions for this scene
            EventManager.OnLandObjectAdded +=
                new EventManager.LandObjectAdded(simDataService.StoreLandObject);
            EventManager.OnLandObjectRemoved +=
                new EventManager.LandObjectRemoved(simDataService.RemoveLandObject);

            m_sceneGraph = new SceneGraph(this);

            // If the scene graph has an Unrecoverable error, restart this sim.
            // Currently the only thing that causes it to happen is two kinds of specific
            // Physics based crashes.
            //
            // Out of memory
            // Operating system has killed the plugin
            m_sceneGraph.UnRecoverableError += RestartNow;

            RegisterDefaultSceneEvents();

            DumpAssetsToFile = dumpAssetsToFile;

            m_scripts_enabled = !RegionInfo.RegionSettings.DisableScripts;

            m_physics_enabled = !RegionInfo.RegionSettings.DisablePhysics;

            m_simulatorVersion = simulatorVersion + " (" + Util.GetRuntimeInformation() + ")";

            #region Region Config

            // Region config overrides global config
            //
            if (m_config.Configs["Startup"] != null)
            {
                IConfig startupConfig = m_config.Configs["Startup"];

                StartDisabled = startupConfig.GetBoolean("StartDisabled", false);

                m_defaultDrawDistance = startupConfig.GetFloat("DefaultDrawDistance", m_defaultDrawDistance);
                m_useBackup = startupConfig.GetBoolean("UseSceneBackup", m_useBackup);
                if (!m_useBackup)
                    m_log.InfoFormat("[SCENE]: Backup has been disabled for {0}", RegionInfo.RegionName);
                
                //Animation states
                m_useFlySlow = startupConfig.GetBoolean("enableflyslow", false);

                PhysicalPrims = startupConfig.GetBoolean("physical_prim", PhysicalPrims);
                CollidablePrims = startupConfig.GetBoolean("collidable_prim", CollidablePrims);

                m_minNonphys = startupConfig.GetFloat("NonPhysicalPrimMin", m_minNonphys);
                if (RegionInfo.NonphysPrimMin > 0)
                {
                    m_minNonphys = RegionInfo.NonphysPrimMin;
                }

                m_maxNonphys = startupConfig.GetFloat("NonPhysicalPrimMax", m_maxNonphys);
                if (RegionInfo.NonphysPrimMax > 0)
                {
                    m_maxNonphys = RegionInfo.NonphysPrimMax;
                }

                m_minPhys = startupConfig.GetFloat("PhysicalPrimMin", m_minPhys);
                if (RegionInfo.PhysPrimMin > 0)
                {
                    m_minPhys = RegionInfo.PhysPrimMin;
                }

                m_maxPhys = startupConfig.GetFloat("PhysicalPrimMax", m_maxPhys);
                if (RegionInfo.PhysPrimMax > 0)
                {
                    m_maxPhys = RegionInfo.PhysPrimMax;
                }

                // Here, if clamping is requested in either global or
                // local config, it will be used
                //
                m_clampPrimSize = startupConfig.GetBoolean("ClampPrimSize", m_clampPrimSize);
                if (RegionInfo.ClampPrimSize)
                {
                    m_clampPrimSize = true;
                }

                m_linksetCapacity = startupConfig.GetInt("LinksetPrims", m_linksetCapacity);
                if (RegionInfo.LinksetCapacity > 0)
                {
                    m_linksetCapacity = RegionInfo.LinksetCapacity;
                }

                m_useTrashOnDelete = startupConfig.GetBoolean("UseTrashOnDelete", m_useTrashOnDelete);
                m_trustBinaries = startupConfig.GetBoolean("TrustBinaries", m_trustBinaries);
                m_allowScriptCrossings = startupConfig.GetBoolean("AllowScriptCrossing", m_allowScriptCrossings);
                m_dontPersistBefore =
                  startupConfig.GetLong("MinimumTimeBeforePersistenceConsidered", DEFAULT_MIN_TIME_FOR_PERSISTENCE);
                m_dontPersistBefore *= 10000000;
                m_persistAfter =
                  startupConfig.GetLong("MaximumTimeBeforePersistenceConsidered", DEFAULT_MAX_TIME_FOR_PERSISTENCE);
                m_persistAfter *= 10000000;

                m_defaultScriptEngine = startupConfig.GetString("DefaultScriptEngine", "XEngine");

                SpawnPointRouting = startupConfig.GetString("SpawnPointRouting", "closest");
                TelehubAllowLandmarks = startupConfig.GetBoolean("TelehubAllowLandmark", false);

                IConfig packetConfig = m_config.Configs["PacketPool"];
                if (packetConfig != null)
                {
                    PacketPool.Instance.RecyclePackets = packetConfig.GetBoolean("RecyclePackets", true);
                    PacketPool.Instance.RecycleDataBlocks = packetConfig.GetBoolean("RecycleDataBlocks", true);
                }

                m_strictAccessControl = startupConfig.GetBoolean("StrictAccessControl", m_strictAccessControl);

                m_generateMaptiles = startupConfig.GetBoolean("GenerateMaptiles", true);
                if (m_generateMaptiles)
                {
                    int maptileRefresh = startupConfig.GetInt("MaptileRefresh", 0);
                    if (maptileRefresh != 0)
                    {
                        m_mapGenerationTimer.Interval = maptileRefresh * 1000;
                        m_mapGenerationTimer.Elapsed += RegenerateMaptileAndReregister;
                        m_mapGenerationTimer.AutoReset = true;
                        m_mapGenerationTimer.Start();
                    }
                }
                else
                {
                    string tile = startupConfig.GetString("MaptileStaticUUID", UUID.Zero.ToString());
                    UUID tileID;

                    if (UUID.TryParse(tile, out tileID))
                    {
                        RegionInfo.RegionSettings.TerrainImageID = tileID;
                    }
                }

                string grant = startupConfig.GetString("AllowedClients", String.Empty);
                if (grant.Length > 0)
                {
                    foreach (string viewer in grant.Split('|'))
                    {
                        m_AllowedViewers.Add(viewer.Trim().ToLower());
                    }
                }

                grant = startupConfig.GetString("BannedClients", String.Empty);
                if (grant.Length > 0)
                {
                    foreach (string viewer in grant.Split('|'))
                    {
                        m_BannedViewers.Add(viewer.Trim().ToLower());
                    }
                }

                MinFrameTime              = startupConfig.GetFloat( "MinFrameTime",                      MinFrameTime);
                m_update_backup           = startupConfig.GetInt(   "UpdateStorageEveryNFrames",         m_update_backup);
                m_update_coarse_locations = startupConfig.GetInt(   "UpdateCoarseLocationsEveryNFrames", m_update_coarse_locations);
                m_update_entitymovement   = startupConfig.GetInt(   "UpdateEntityMovementEveryNFrames",  m_update_entitymovement);
                m_update_events           = startupConfig.GetInt(   "UpdateEventsEveryNFrames",          m_update_events);
                m_update_objects          = startupConfig.GetInt(   "UpdateObjectsEveryNFrames",         m_update_objects);
                m_update_physics          = startupConfig.GetInt(   "UpdatePhysicsEveryNFrames",         m_update_physics);
                m_update_presences        = startupConfig.GetInt(   "UpdateAgentsEveryNFrames",          m_update_presences);
                m_update_terrain          = startupConfig.GetInt(   "UpdateTerrainEveryNFrames",         m_update_terrain);
                m_update_temp_cleaning    = startupConfig.GetInt(   "UpdateTempCleaningEveryNFrames",    m_update_temp_cleaning);
            }

            // FIXME: Ultimately this should be in a module.
            SendPeriodicAppearanceUpdates = true;
            
            IConfig appearanceConfig = m_config.Configs["Appearance"];
            if (appearanceConfig != null)
            {
                SendPeriodicAppearanceUpdates
                    = appearanceConfig.GetBoolean("ResendAppearanceUpdates", SendPeriodicAppearanceUpdates);
            }

            #endregion Region Config

            #region Interest Management

            IConfig interestConfig = m_config.Configs["InterestManagement"];
            if (interestConfig != null)
            {
                string update_prioritization_scheme = interestConfig.GetString("UpdatePrioritizationScheme", "Time").Trim().ToLower();

                try
                {
                    m_priorityScheme = (UpdatePrioritizationSchemes)Enum.Parse(typeof(UpdatePrioritizationSchemes), update_prioritization_scheme, true);
                }
                catch (Exception)
                {
                    m_log.Warn("[PRIORITIZER]: UpdatePrioritizationScheme was not recognized, setting to default prioritizer Time");
                    m_priorityScheme = UpdatePrioritizationSchemes.Time;
                }

                m_reprioritizationEnabled = interestConfig.GetBoolean("ReprioritizationEnabled", true);
                m_reprioritizationInterval = interestConfig.GetDouble("ReprioritizationInterval", 5000.0);
                m_rootReprioritizationDistance = interestConfig.GetDouble("RootReprioritizationDistance", 10.0);
                m_childReprioritizationDistance = interestConfig.GetDouble("ChildReprioritizationDistance", 20.0);
            }

            m_log.DebugFormat("[SCENE]: Using the {0} prioritization scheme", m_priorityScheme);

            #endregion Interest Management

            StatsReporter = new SimStatsReporter(this);
            StatsReporter.OnSendStatsResult += SendSimStatsPackets;
            StatsReporter.OnStatsIncorrect += m_sceneGraph.RecalculateStats;
        }

        public Scene(RegionInfo regInfo) : base(regInfo)
        {
            PhysicalPrims = true;
            CollidablePrims = true;

            BordersLocked = true;
            Border northBorder = new Border();
            northBorder.BorderLine = new Vector3(float.MinValue, float.MaxValue, (int)Constants.RegionSize);  //<---
            northBorder.CrossDirection = Cardinals.N;
            NorthBorders.Add(northBorder);

            Border southBorder = new Border();
            southBorder.BorderLine = new Vector3(float.MinValue, float.MaxValue,0);    //--->
            southBorder.CrossDirection = Cardinals.S;
            SouthBorders.Add(southBorder);

            Border eastBorder = new Border();
            eastBorder.BorderLine = new Vector3(float.MinValue, float.MaxValue, (int)Constants.RegionSize);   //<---
            eastBorder.CrossDirection = Cardinals.E;
            EastBorders.Add(eastBorder);

            Border westBorder = new Border();
            westBorder.BorderLine = new Vector3(float.MinValue, float.MaxValue,0);     //--->
            westBorder.CrossDirection = Cardinals.W;
            WestBorders.Add(westBorder);
            BordersLocked = false;

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
            get { return m_sceneGraph; }
        }

        protected virtual void RegisterDefaultSceneEvents()
        {
            IDialogModule dm = RequestModuleInterface<IDialogModule>();

            if (dm != null)
                m_eventManager.OnPermissionError += dm.SendAlertToUser;

            m_eventManager.OnSignificantClientMovement += HandleOnSignificantClientMovement;
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
            uint xcell = (uint)((int)otherRegion.RegionLocX / (int)Constants.RegionSize);
            uint ycell = (uint)((int)otherRegion.RegionLocY / (int)Constants.RegionSize);
            //m_log.InfoFormat("[SCENE]: (on region {0}): Region {1} up in coords {2}-{3}", 
            //    RegionInfo.RegionName, otherRegion.RegionName, xcell, ycell);

            if (RegionInfo.RegionHandle != otherRegion.RegionHandle)
            {
                // If these are cast to INT because long + negative values + abs returns invalid data
                int resultX = Math.Abs((int)xcell - (int)RegionInfo.RegionLocX);
                int resultY = Math.Abs((int)ycell - (int)RegionInfo.RegionLocY);
                if (resultX <= 1 && resultY <= 1)
                {
                    // Let the grid service module know, so this can be cached
                    m_eventManager.TriggerOnRegionUp(otherRegion);

                    try
                    {
                        ForEachRootScenePresence(delegate(ScenePresence agent)
                        {
                            //agent.ControllingClient.new
                            //this.CommsManager.InterRegion.InformRegionOfChildAgent(otherRegion.RegionHandle, agent.ControllingClient.RequestClientInfo());

                            List<ulong> old = new List<ulong>();
                            old.Add(otherRegion.RegionHandle);
                            agent.DropOldNeighbours(old);
                            if (EntityTransferModule != null && agent.PresenceType != PresenceType.Npc)
                                EntityTransferModule.EnableChildAgent(agent, otherRegion);
                        });
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
                    m_log.InfoFormat(
                        "[SCENE]: Got notice about far away Region: {0} at ({1}, {2})",
                        otherRegion.RegionName, otherRegion.RegionLocX, otherRegion.RegionLocY);
                }
            }
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
        /// Checks whether this region has a neighbour in the given direction.
        /// </summary>
        /// <param name="car"></param>
        /// <param name="fix"></param>
        /// <returns>
        /// An integer which represents a compass point.  N == 1, going clockwise until we reach NW == 8.
        /// Returns a positive integer if there is a region in that direction, a negative integer if not.
        /// </returns>
        public int HaveNeighbor(Cardinals car, ref int[] fix)
        {
            uint neighbourx = RegionInfo.RegionLocX;
            uint neighboury = RegionInfo.RegionLocY;

            int dir = (int)car;

            if (dir > 1 && dir < 5) //Heading East
                neighbourx++;
            else if (dir > 5) // Heading West
                neighbourx--;

            if (dir < 3 || dir == 8) // Heading North
                neighboury++;
            else if (dir > 3 && dir < 7) // Heading Sout
                neighboury--;

            int x = (int)(neighbourx * Constants.RegionSize);
            int y = (int)(neighboury * Constants.RegionSize);
            GridRegion neighbourRegion = GridService.GetRegionByPosition(RegionInfo.ScopeID, x, y);

            if (neighbourRegion == null)
            {
                fix[0] = (int)(RegionInfo.RegionLocX - neighbourx);
                fix[1] = (int)(RegionInfo.RegionLocY - neighboury);
                return dir * (-1);
            }
            else
                return dir;
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
            if (startupConfig != null)
            {
                if (startupConfig.GetBoolean("InworldRestartShutsDown", false))
                {
                    MainConsole.Instance.RunCommand("shutdown");
                    return;
                }
            }

            m_log.Error("[REGION]: Closing");
            Close();

            if (PhysicsScene != null)
            {
                PhysicsScene.Dispose();
            }            

            m_log.Error("[REGION]: Firing Region Restart Message");

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
                foreach (RegionInfo region in m_regionRestartNotifyList)
                {
                    GridRegion r = new GridRegion(region);
                    try
                    {
                        ForEachRootScenePresence(delegate(ScenePresence agent)
                        {
                            if (EntityTransferModule != null && agent.PresenceType != PresenceType.Npc)
                                EntityTransferModule.EnableChildAgent(agent, r);
                        });
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

        public void SetSceneCoreDebug(Dictionary<string, string> options)
        {
            if (options.ContainsKey("scripting"))
            {
                bool enableScripts = true;
                if (bool.TryParse(options["scripting"], out enableScripts) && m_scripts_enabled != enableScripts)
                {
                    if (!enableScripts)
                    {
                        m_log.Info("Stopping all Scripts in Scene");
                        
                        EntityBase[] entities = Entities.GetEntities();
                        foreach (EntityBase ent in entities)
                        {
                            if (ent is SceneObjectGroup)
                                ((SceneObjectGroup)ent).RemoveScriptInstances(false);
                        }
                    }
                    else
                    {
                        m_log.Info("Starting all Scripts in Scene");
    
                        EntityBase[] entities = Entities.GetEntities();
                        foreach (EntityBase ent in entities)
                        {
                            if (ent is SceneObjectGroup)
                            {
                                SceneObjectGroup sog = (SceneObjectGroup)ent;
                                sog.CreateScriptInstances(0, false, DefaultScriptEngine, 0);
                                sog.ResumeScripts();
                            }
                        }
                    }

                    m_scripts_enabled = enableScripts;
                }
            }

            if (options.ContainsKey("physics"))
            {
                bool enablePhysics;
                if (bool.TryParse(options["physics"], out enablePhysics))
                    m_physics_enabled = enablePhysics;
            }

//            if (options.ContainsKey("collisions"))
//            {
//                // TODO: Implement.  If false, should stop objects colliding, though possibly should still allow
//                // the avatar themselves to collide with the ground.
//            }

            if (options.ContainsKey("teleport"))
            {
                bool enableTeleportDebugging;
                if (bool.TryParse(options["teleport"], out enableTeleportDebugging))
                    DebugTeleporting = enableTeleportDebugging;
            }

            if (options.ContainsKey("updates"))
            {
                bool enableUpdateDebugging;
                if (bool.TryParse(options["updates"], out enableUpdateDebugging))
                {
                    DebugUpdates = enableUpdateDebugging;
                    GcNotify.Enabled = DebugUpdates;
                }
            }
        }

        public int GetInaccurateNeighborCount()
        {
            return m_neighbours.Count;
        }

        // This is the method that shuts down the scene.
        public override void Close()
        {
            m_log.InfoFormat("[SCENE]: Closing down the single simulator: {0}", RegionInfo.RegionName);

            StatsReporter.Close();

            m_restartTimer.Stop();
            m_restartTimer.Close();

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
            ForEachScenePresence(delegate(ScenePresence avatar) { avatar.ControllingClient.Close(); });

            m_log.Debug("[SCENE]: Persisting changed objects");
            EventManager.TriggerSceneShuttingDown(this);

            EntityBase[] entities = GetEntities();
            foreach (EntityBase entity in entities)
            {
                if (!entity.IsDeleted && entity is SceneObjectGroup && ((SceneObjectGroup)entity).HasGroupChanged)
                {
                    ((SceneObjectGroup)entity).ProcessBackup(SimulationDataService, false);
                }
            }

            m_sceneGraph.Close();

            if (PhysicsScene != null)
            {
                PhysicsScene phys = PhysicsScene;
                // remove the physics engine from both Scene and SceneGraph
                PhysicsScene = null;
                phys.Dispose();
                phys = null;
            }

            if (!GridService.DeregisterRegion(RegionInfo.RegionID))
                m_log.WarnFormat("[SCENE]: Deregister from grid failed for region {0}", Name);

            // call the base class Close method.
            base.Close();
        }

        /// <summary>
        /// Start the scene
        /// </summary>
        public void Start()
        {
//            m_log.DebugFormat("[SCENE]: Starting Heartbeat timer for {0}", RegionInfo.RegionName);

            //m_heartbeatTimer.Enabled = true;
            //m_heartbeatTimer.Interval = (int)(m_timespan * 1000);
            //m_heartbeatTimer.Elapsed += new ElapsedEventHandler(Heartbeat);
            if (m_heartbeatThread != null)
            {
                m_heartbeatThread.Abort();
                m_heartbeatThread = null;
            }
//            m_lastUpdate = Util.EnvironmentTickCount();

            m_heartbeatThread
                = Watchdog.StartThread(
                    Heartbeat, string.Format("Heartbeat ({0})", RegionInfo.RegionName), ThreadPriority.Normal, false, false);
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
        /// Performs per-frame updates regularly
        /// </summary>
        private void Heartbeat()
        {
//            if (!Monitor.TryEnter(m_heartbeatLock))
//            {
//                Watchdog.RemoveThread();
//                return;
//            }

//            try
//            {

            m_eventManager.TriggerOnRegionStarted(this);

            // The first frame can take a very long time due to physics actors being added on startup.  Therefore,
            // don't turn on the watchdog alarm for this thread until the second frame, in order to prevent false
            // alarms for scenes with many objects.
            Update(1);

            Watchdog.StartThread(
                    Maintenance, string.Format("Maintenance ({0})", RegionInfo.RegionName), ThreadPriority.Normal, false, true);

            Watchdog.GetCurrentThreadInfo().AlarmIfTimeout = true;
            Update(-1);

//                m_lastUpdate = Util.EnvironmentTickCount();
//                m_firstHeartbeat = false;
//            }
//            finally
//            {
//                Monitor.Pulse(m_heartbeatLock);
//                Monitor.Exit(m_heartbeatLock);
//            }

            Watchdog.RemoveThread();
        }

        private void Maintenance()
        {
            DoMaintenance(-1);

            Watchdog.RemoveThread();
        }

        public void DoMaintenance(int runs)
        {
            long? endRun = null;
            int runtc;
            int previousMaintenanceTick;

            if (runs >= 0)
                endRun = MaintenanceRun + runs;

            List<Vector3> coarseLocations;
            List<UUID> avatarUUIDs;

            while (!m_shuttingDown && (endRun == null || MaintenanceRun < endRun))
            {
                runtc = Util.EnvironmentTickCount();
                ++MaintenanceRun;

                // Coarse locations relate to positions of green dots on the mini-map (on a SecondLife client)
                if (MaintenanceRun % (m_update_coarse_locations / 10) == 0)
                {
                    SceneGraph.GetCoarseLocations(out coarseLocations, out avatarUUIDs, 60);
                    // Send coarse locations to clients
                    ForEachScenePresence(delegate(ScenePresence presence)
                    {
                        presence.SendCoarseLocations(coarseLocations, avatarUUIDs);
                    });
                }

                if (SendPeriodicAppearanceUpdates && MaintenanceRun % 60 == 0)
                {
//                    m_log.DebugFormat("[SCENE]: Sending periodic appearance updates");

                    if (AvatarFactory != null)
                    {
                        ForEachRootScenePresence(sp => AvatarFactory.SendAppearance(sp.UUID));
                    }
                }

                Watchdog.UpdateThread();

                previousMaintenanceTick = m_lastMaintenanceTick;
                m_lastMaintenanceTick = Util.EnvironmentTickCount();
                runtc = Util.EnvironmentTickCountSubtract(m_lastMaintenanceTick, runtc);
                runtc = (int)(MinMaintenanceTime * 1000) - runtc;
    
                if (runtc > 0)
                    Thread.Sleep(runtc);
    
                // Optionally warn if a frame takes double the amount of time that it should.
                if (DebugUpdates
                    && Util.EnvironmentTickCountSubtract(
                        m_lastMaintenanceTick, previousMaintenanceTick) > (int)(MinMaintenanceTime * 1000 * 2))
                    m_log.WarnFormat(
                        "[SCENE]: Maintenance took {0} ms (desired max {1} ms) in {2}",
                        Util.EnvironmentTickCountSubtract(m_lastMaintenanceTick, previousMaintenanceTick),
                        MinMaintenanceTime * 1000,
                        RegionInfo.RegionName);
            }
        }

        public override void Update(int frames)
        {
            long? endFrame = null;

            if (frames >= 0)
                endFrame = Frame + frames;

            float physicsFPS = 0f;
            int previousFrameTick, tmpMS;
            int maintc = Util.EnvironmentTickCount();

            while (!m_shuttingDown && (endFrame == null || Frame < endFrame))
            {
                ++Frame;

//            m_log.DebugFormat("[SCENE]: Processing frame {0} in {1}", Frame, RegionInfo.RegionName);

                agentMS = tempOnRezMS = eventMS = backupMS = terrainMS = landMS = spareMS = 0;

                try
                {
                    // Apply taints in terrain module to terrain in physics scene
                    if (Frame % m_update_terrain == 0)
                    {
                        tmpMS = Util.EnvironmentTickCount();
                        UpdateTerrain();
                        terrainMS = Util.EnvironmentTickCountSubtract(tmpMS);
                    }

                    tmpMS = Util.EnvironmentTickCount();
                    if ((Frame % m_update_physics == 0) && m_physics_enabled)
                        m_sceneGraph.UpdatePreparePhysics();
                    physicsMS2 = Util.EnvironmentTickCountSubtract(tmpMS);
    
                    // Apply any pending avatar force input to the avatar's velocity
                    tmpMS = Util.EnvironmentTickCount();
                    if (Frame % m_update_entitymovement == 0)
                        m_sceneGraph.UpdateScenePresenceMovement();
                    agentMS = Util.EnvironmentTickCountSubtract(tmpMS);
    
                    // Perform the main physics update.  This will do the actual work of moving objects and avatars according to their
                    // velocity
                    tmpMS = Util.EnvironmentTickCount();
                    if (Frame % m_update_physics == 0)
                    {
                        if (m_physics_enabled)
                            physicsFPS = m_sceneGraph.UpdatePhysics(MinFrameTime);
    
                        if (SynchronizeScene != null)
                            SynchronizeScene(this);
                    }
                    physicsMS = Util.EnvironmentTickCountSubtract(tmpMS);

                    tmpMS = Util.EnvironmentTickCount();
    
                    // Check if any objects have reached their targets
                    CheckAtTargets();
    
                    // Update SceneObjectGroups that have scheduled themselves for updates
                    // Objects queue their updates onto all scene presences
                    if (Frame % m_update_objects == 0)
                        m_sceneGraph.UpdateObjectGroups();
    
                    // Run through all ScenePresences looking for updates
                    // Presence updates and queued object updates for each presence are sent to clients
                    if (Frame % m_update_presences == 0)
                        m_sceneGraph.UpdatePresences();
    
                    agentMS += Util.EnvironmentTickCountSubtract(tmpMS);
    
                    // Delete temp-on-rez stuff
                    if (Frame % m_update_temp_cleaning == 0 && !m_cleaningTemps)
                    {
                        tmpMS = Util.EnvironmentTickCount();
                        m_cleaningTemps = true;
                        Util.FireAndForget(delegate { CleanTempObjects(); m_cleaningTemps = false;  });
                        tempOnRezMS = Util.EnvironmentTickCountSubtract(tmpMS);
                    }
    
                    if (Frame % m_update_events == 0)
                    {
                        tmpMS = Util.EnvironmentTickCount();
                        UpdateEvents();
                        eventMS = Util.EnvironmentTickCountSubtract(tmpMS);
                    }
    
                    if (Frame % m_update_backup == 0)
                    {
                        tmpMS = Util.EnvironmentTickCount();
                        UpdateStorageBackup();
                        backupMS = Util.EnvironmentTickCountSubtract(tmpMS);
                    }
    
                    //if (Frame % m_update_land == 0)
                    //{
                    //    int ldMS = Util.EnvironmentTickCount();
                    //    UpdateLand();
                    //    landMS = Util.EnvironmentTickCountSubtract(ldMS);
                    //}
    
                    if (!LoginsEnabled && Frame == 20)
                    {
    //                    m_log.DebugFormat("{0} {1} {2}", LoginsDisabled, m_sceneGraph.GetActiveScriptsCount(), LoginLock);
    
                        // In 99.9% of cases it is a bad idea to manually force garbage collection. However,
                        // this is a rare case where we know we have just went through a long cycle of heap
                        // allocations, and there is no more work to be done until someone logs in
                        GC.Collect();

                        if (!LoginLock)
                        {
                            if (!StartDisabled)
                            {
                                m_log.InfoFormat("[REGION]: Enabling logins for {0}", RegionInfo.RegionName);
                                LoginsEnabled = true;
                            }

                            m_sceneGridService.InformNeighborsThatRegionisUp(
                                RequestModuleInterface<INeighbourService>(), RegionInfo);

                            // Region ready should always be set
                            Ready = true;
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
                    m_log.ErrorFormat(
                        "[SCENE]: Failed on region {0} with exception {1}{2}",
                        RegionInfo.RegionName, e.Message, e.StackTrace);
                }
    
                EventManager.TriggerRegionHeartbeatEnd(this);

                Watchdog.UpdateThread();

                previousFrameTick = m_lastFrameTick;
                m_lastFrameTick = Util.EnvironmentTickCount();
                tmpMS = Util.EnvironmentTickCountSubtract(m_lastFrameTick, maintc);
                tmpMS = (int)(MinFrameTime * 1000) - tmpMS;

                if (tmpMS > 0)
                {
                    Thread.Sleep(tmpMS);
                    spareMS += tmpMS;
                }

                frameMS = Util.EnvironmentTickCountSubtract(maintc);
                maintc = Util.EnvironmentTickCount();

                otherMS = tempOnRezMS + eventMS + backupMS + terrainMS + landMS;

                // if (Frame%m_update_avatars == 0)
                //   UpdateInWorldTime();
                StatsReporter.AddPhysicsFPS(physicsFPS);
                StatsReporter.AddTimeDilation(TimeDilation);
                StatsReporter.AddFPS(1);

                StatsReporter.addFrameMS(frameMS);
                StatsReporter.addAgentMS(agentMS);
                StatsReporter.addPhysicsMS(physicsMS + physicsMS2);
                StatsReporter.addOtherMS(otherMS);
                StatsReporter.AddSpareMS(spareMS);
                StatsReporter.addScriptLines(m_sceneGraph.GetScriptLPS());

               // Optionally warn if a frame takes double the amount of time that it should.
                if (DebugUpdates
                    && Util.EnvironmentTickCountSubtract(
                        m_lastFrameTick, previousFrameTick) > (int)(MinFrameTime * 1000 * 2))
                    m_log.WarnFormat(
                        "[SCENE]: Frame took {0} ms (desired max {1} ms) in {2}",
                        Util.EnvironmentTickCountSubtract(m_lastFrameTick, previousFrameTick),
                        MinFrameTime * 1000,
                        RegionInfo.RegionName);
            }
        }        

        public void AddGroupTarget(SceneObjectGroup grp)
        {
            lock (m_groupsWithTargets)
                m_groupsWithTargets[grp.UUID] = grp;
        }

        public void RemoveGroupTarget(SceneObjectGroup grp)
        {
            lock (m_groupsWithTargets)
                m_groupsWithTargets.Remove(grp.UUID);
        }

        private void CheckAtTargets()
        {
            Dictionary<UUID, SceneObjectGroup>.ValueCollection objs;
            lock (m_groupsWithTargets)
                objs = m_groupsWithTargets.Values;

            foreach (SceneObjectGroup entry in objs)
                entry.checkAtTargets();
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

        /// <summary>
        /// Back up queued up changes
        /// </summary>
        private void UpdateStorageBackup()
        {
            if (!m_backingup)
            {
                m_backingup = true;
                Util.FireAndForget(BackupWaitCallback);
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
        /// Wrapper for Backup() that can be called with Util.FireAndForget()
        /// </summary>
        private void BackupWaitCallback(object o)
        {
            Backup(false);
        }
        
        /// <summary>
        /// Backup the scene.  This acts as the main method of the backup thread.
        /// </summary>
        /// <param name="forced">
        /// If true, then any changes that have not yet been persisted are persisted.  If false,
        /// then the persistence decision is left to the backup code (in some situations, such as object persistence,
        /// it's much more efficient to backup multiple changes at once rather than every single one).
        /// <returns></returns>
        public void Backup(bool forced)
        {
            lock (m_returns)
            {
                EventManager.TriggerOnBackup(SimulationDataService, forced);
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
                    msg.offline = (byte)0;
                    msg.ParentEstateID = RegionInfo.EstateSettings.ParentEstateID;
                    msg.Position = Vector3.Zero;
                    msg.RegionID = RegionInfo.RegionID.Guid;

                    // We must fill in a null-terminated 'empty' string here since bytes[0] will crash viewer 3.
                    msg.binaryBucket = Util.StringToBytes256("\0");
                    if (ret.Value.count > 1)
                        msg.message = string.Format("Your {0} objects were returned from {1} in region {2} due to {3}", ret.Value.count, ret.Value.location.ToString(), RegionInfo.RegionName, ret.Value.reason);
                    else
                        msg.message = string.Format("Your object {0} was returned from {1} in region {2} due to {3}", ret.Value.objectName, ret.Value.location.ToString(), RegionInfo.RegionName, ret.Value.reason);

                    IMessageTransferModule tr = RequestModuleInterface<IMessageTransferModule>();
                    if (tr != null)
                        tr.SendInstantMessage(msg, delegate(bool success) {});
                }
                m_returns.Clear();
            }
        }

        /// <summary>
        /// Synchronous force backup.  For deletes and links/unlinks
        /// </summary>
        /// <param name="group">Object to be backed up</param>
        public void ForceSceneObjectBackup(SceneObjectGroup group)
        {
            if (group != null)
            {
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

        /// <summary>
        /// Store the terrain in the persistant data store
        /// </summary>
        public void SaveTerrain()
        {
            SimulationDataService.StoreTerrain(Heightmap.GetDoubles(), RegionInfo.RegionID);
        }

        public void StoreWindlightProfile(RegionLightShareData wl)
        {
            RegionInfo.WindlightSettings = wl;
            SimulationDataService.StoreRegionWindlightSettings(wl);
            m_eventManager.TriggerOnSaveNewWindlightProfile();
        }

        public void LoadWindlightProfile()
        {
            RegionInfo.WindlightSettings = SimulationDataService.LoadRegionWindlightSettings(RegionInfo.RegionID);
            m_eventManager.TriggerOnSaveNewWindlightProfile();
        }

        /// <summary>
        /// Loads the World heightmap
        /// </summary>
        public override void LoadWorldMap()
        {
            try
            {
                double[,] map = SimulationDataService.LoadTerrain(RegionInfo.RegionID);
                if (map == null)
                {
                    // This should be in the Terrain module, but it isn't because
                    // the heightmap is needed _way_ before the modules are initialized...
                    IConfig terrainConfig = m_config.Configs["Terrain"];
                    String m_InitialTerrain = "pinhead-island";
                    if (terrainConfig != null)
                        m_InitialTerrain = terrainConfig.GetString("InitialTerrain", m_InitialTerrain);

                    m_log.InfoFormat("[TERRAIN]: No default terrain. Generating a new terrain {0}.", m_InitialTerrain);
                    Heightmap = new TerrainChannel(m_InitialTerrain);

                    SimulationDataService.StoreTerrain(Heightmap.GetDoubles(), RegionInfo.RegionID);
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
                
                // Non standard region size.    If there's an old terrain in the database, it might read past the buffer
                #pragma warning disable 0162
                if ((int)Constants.RegionSize != 256)
                {
                    Heightmap = new TerrainChannel();

                    SimulationDataService.StoreTerrain(Heightmap.GetDoubles(), RegionInfo.RegionID);
                }
            }
            catch (Exception e)
            {
                m_log.WarnFormat(
                    "[TERRAIN]: Scene.cs: LoadWorldMap() - Failed with exception {0}{1}", e.Message, e.StackTrace);
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

            GridRegion region = new GridRegion(RegionInfo);
            string error = GridService.RegisterRegion(RegionInfo.ScopeID, region);
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
                EventManager.TriggerOnSceneObjectLoaded(group);
                SceneObjectPart rootPart = group.GetPart(group.UUID);
                rootPart.Flags &= ~PrimFlags.Scripted;
                rootPart.TrimPermissions();

                // Don't do this here - it will get done later on when sculpt data is loaded.
//                group.CheckSculptAndLoad();
            }

            LoadingPrims = false;
            EventManager.TriggerPrimsLoaded(this);
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

                        //Seems to make no sense to do this as this call is used for rezzing from inventory as well, and with inventory items their size is not always 0.5f
                        //And in cases when we weren't rezzing from inventory we were re-adding the 0.25 straight after calling this method
                        // Un-offset the prim (it gets offset later by the consumer method)
                        //pos.Z -= 0.25F; 
                       
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

                //increase height so its above the ground.
                //should be getting the normal of the ground at the rez point and using that?
                pos.Z += scale.Z / 2f;
                return pos;
            }
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
                                       byte RayEndIsIntersection)
        {
            Vector3 pos = GetNewRezLocation(RayStart, RayEnd, RayTargetID, rot, bypassRaycast, RayEndIsIntersection, true, new Vector3(0.5f, 0.5f, 0.5f), false);

            if (Permissions.CanRezObject(1, ownerID, pos))
            {
                // rez ON the ground, not IN the ground
                // pos.Z += 0.25F; The rez point should now be correct so that its not in the ground

                AddNewPrim(ownerID, groupID, pos, rot, shape);
            }
            else
            {
                IClientAPI client = null;
                if (TryGetClient(ownerID, out client))
                    client.SendAlertMessage("You cannot create objects here.");
            }
        }

        public virtual SceneObjectGroup AddNewPrim(
            UUID ownerID, UUID groupID, Vector3 pos, Quaternion rot, PrimitiveBaseShape shape)
        {
            //m_log.DebugFormat(
            //    "[SCENE]: Scene.AddNewPrim() pcode {0} called for {1} in {2}", shape.PCode, ownerID, RegionInfo.RegionName);

            SceneObjectGroup sceneObject = null;
            
            // If an entity creator has been registered for this prim type then use that
            if (m_entityCreators.ContainsKey((PCode)shape.PCode))
            {
                sceneObject = m_entityCreators[(PCode)shape.PCode].CreateEntity(ownerID, groupID, pos, rot, shape);
            }
            else
            {
                // Otherwise, use this default creation code;
                sceneObject = new SceneObjectGroup(ownerID, pos, rot, shape);
                AddNewSceneObject(sceneObject, true);
                sceneObject.SetGroup(groupID, null);
            }

            if (UserManagementModule != null)
                sceneObject.RootPart.CreatorIdentification = UserManagementModule.GetUserUUI(ownerID);

            sceneObject.ScheduleGroupForFullUpdate();

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
            return m_sceneGraph.AddRestoredSceneObject(sceneObject, attachToBackup, alreadyPersisted, sendClientUpdates);
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
            lock (Entities)
            {
                EntityBase[] entities = Entities.GetEntities();
                foreach (EntityBase e in entities)
                {
                    if (e is SceneObjectGroup)
                    {
                        SceneObjectGroup sog = (SceneObjectGroup)e;
                        if (!sog.IsAttachment)
                            DeleteSceneObject((SceneObjectGroup)e, false);
                    }
                }
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
//            m_log.DebugFormat("[SCENE]: Deleting scene object {0} {1}", group.Name, group.UUID);

            if (removeScripts)
                group.RemoveScriptInstances(true);
            else
                group.StopScriptInstances();

            SceneObjectPart[] partList = group.Parts;

            foreach (SceneObjectPart part in partList)
            {
                if (part.IsJoint() && ((part.Flags & PrimFlags.Physics) != 0))
                {
                    PhysicsScene.RequestJointDeletion(part.Name); // FIXME: what if the name changed?
                }
                else if (part.PhysActor != null)
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

//            m_log.DebugFormat("[SCENE]: Exit DeleteSceneObject() for {0} {1}", group.Name, group.UUID);            
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
                    // Force a database update so that the scene object group ID is accurate.  It's possible that the
                    // group has recently been delinked from another group but that this change has not been persisted
                    // to the DB.
                    // This is an expensive thing to do so only do it if absolutely necessary.
                    if (so.HasGroupChangedDueToDelink)
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

        /// <summary>
        /// Move the given scene object into a new region depending on which region its absolute position has moved
        /// into.
        ///
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

            if (EntityTransferModule != null)
                EntityTransferModule.Cross(grp, attemptedPosition, silent);
        }

        public Border GetCrossedBorder(Vector3 position, Cardinals gridline)
        {
            if (BordersLocked)
            {
                switch (gridline)
                {
                    case Cardinals.N:
                        lock (NorthBorders)
                        {
                            foreach (Border b in NorthBorders)
                            {
                                if (b.TestCross(position))
                                    return b;
                            }
                        }
                        break;
                    case Cardinals.S:
                        lock (SouthBorders)
                        {
                            foreach (Border b in SouthBorders)
                            {
                                if (b.TestCross(position))
                                    return b;
                            }
                        }

                        break;
                    case Cardinals.E:
                        lock (EastBorders)
                        {
                            foreach (Border b in EastBorders)
                            {
                                if (b.TestCross(position))
                                    return b;
                            }
                        }

                        break;
                    case Cardinals.W:

                        lock (WestBorders)
                        {
                            foreach (Border b in WestBorders)
                            {
                                if (b.TestCross(position))
                                    return b;
                            }
                        }
                        break;

                }
            }
            else
            {
                switch (gridline)
                {
                    case Cardinals.N:
                        foreach (Border b in NorthBorders)
                        {
                            if (b.TestCross(position))
                                return b;
                        }
                       
                        break;
                    case Cardinals.S:
                        foreach (Border b in SouthBorders)
                        {
                            if (b.TestCross(position))
                                return b;
                        }
                        break;
                    case Cardinals.E:
                        foreach (Border b in EastBorders)
                        {
                            if (b.TestCross(position))
                                return b;
                        }

                        break;
                    case Cardinals.W:
                        foreach (Border b in WestBorders)
                        {
                            if (b.TestCross(position))
                                return b;
                        }
                        break;

                }
            }
            

            return null;
        }

        public bool TestBorderCross(Vector3 position, Cardinals border)
        {
            if (BordersLocked)
            {
                switch (border)
                {
                    case Cardinals.N:
                        lock (NorthBorders)
                        {
                            foreach (Border b in NorthBorders)
                            {
                                if (b.TestCross(position))
                                    return true;
                            }
                        }
                        break;
                    case Cardinals.E:
                        lock (EastBorders)
                        {
                            foreach (Border b in EastBorders)
                            {
                                if (b.TestCross(position))
                                    return true;
                            }
                        }
                        break;
                    case Cardinals.S:
                        lock (SouthBorders)
                        {
                            foreach (Border b in SouthBorders)
                            {
                                if (b.TestCross(position))
                                    return true;
                            }
                        }
                        break;
                    case Cardinals.W:
                        lock (WestBorders)
                        {
                            foreach (Border b in WestBorders)
                            {
                                if (b.TestCross(position))
                                    return true;
                            }
                        }
                        break;
                }
            }
            else
            {
                switch (border)
                {
                    case Cardinals.N:
                        foreach (Border b in NorthBorders)
                        {
                            if (b.TestCross(position))
                                return true;
                        }
                        break;
                    case Cardinals.E:
                        foreach (Border b in EastBorders)
                        {
                            if (b.TestCross(position))
                                return true;
                        }
                        break;
                    case Cardinals.S:
                        foreach (Border b in SouthBorders)
                        {
                            if (b.TestCross(position))
                                return true;
                        }
                        break;
                    case Cardinals.W:
                        foreach (Border b in WestBorders)
                        {
                            if (b.TestCross(position))
                                return true;
                        }
                        break;
                }
            }
            return false;
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

            // If the user is banned, we won't let any of their objects
            // enter. Period.
            //
            if (RegionInfo.EstateSettings.IsBanned(newObject.OwnerID))
            {
                m_log.InfoFormat("[INTERREGION]: Denied prim crossing for banned avatar {0}", newObject.OwnerID);
                return false;
            }

            if (newPosition != Vector3.Zero)
                newObject.RootPart.GroupPosition = newPosition;

            if (!AddSceneObject(newObject))
            {
                m_log.DebugFormat(
                    "[INTERREGION]: Problem adding scene object {0} in {1} ", newObject.UUID, RegionInfo.RegionName);
                return false;
            }

            if (!newObject.IsAttachment)
            {
                // FIXME: It would be better to never add the scene object at all rather than add it and then delete
                // it
                if (!Permissions.CanObjectEntry(newObject.UUID, true, newObject.AbsolutePosition))
                {
                    // Deny non attachments based on parcel settings
                    //
                    m_log.Info("[INTERREGION]: Denied prim crossing because of parcel settings");

                    DeleteSceneObject(newObject, false);

                    return false;
                }

                // For attachments, we need to wait until the agent is root
                // before we restart the scripts, or else some functions won't work.
                newObject.RootPart.ParentGroup.CreateScriptInstances(0, false, DefaultScriptEngine, GetStateSource(newObject));
                newObject.ResumeScripts();
            }

            // Do this as late as possible so that listeners have full access to the incoming object
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
            // Force allocation of new LocalId
            //
            SceneObjectPart[] parts = sceneObject.Parts;
            for (int i = 0; i < parts.Length; i++)
                parts[i].LocalId = 0;

            if (sceneObject.IsAttachmentCheckFull()) // Attachment
            {
                sceneObject.RootPart.AddFlag(PrimFlags.TemporaryOnRez);
                sceneObject.RootPart.AddFlag(PrimFlags.Phantom);
                      
                // Don't sent a full update here because this will cause full updates to be sent twice for 
                // attachments on region crossings, resulting in viewer glitches.
                AddRestoredSceneObject(sceneObject, false, false, false);

                // Handle attachment special case
                SceneObjectPart RootPrim = sceneObject.RootPart;

                // Fix up attachment Parent Local ID
                ScenePresence sp = GetScenePresence(sceneObject.OwnerID);

                if (sp != null)
                {
                    SceneObjectGroup grp = sceneObject;

//                    m_log.DebugFormat(
//                        "[ATTACHMENT]: Received attachment {0}, inworld asset id {1}", grp.FromItemID, grp.UUID);
//                    m_log.DebugFormat(
//                        "[ATTACHMENT]: Attach to avatar {0} at position {1}", sp.UUID, grp.AbsolutePosition);

                    RootPrim.RemFlag(PrimFlags.TemporaryOnRez);
                    
                    if (AttachmentsModule != null)
                        AttachmentsModule.AttachObject(sp, grp, 0, false, false);
                }
                else
                {
                    RootPrim.RemFlag(PrimFlags.TemporaryOnRez);
                    RootPrim.AddFlag(PrimFlags.TemporaryOnRez);
                }
            }
            else
            {
                AddRestoredSceneObject(sceneObject, true, false);
            }

            return true;
        }

        private int GetStateSource(SceneObjectGroup sog)
        {
            ScenePresence sp = GetScenePresence(sog.OwnerID);

            if (sp != null)
                return sp.GetStateSource();

            return 2; // StateSource.PrimCrossing
        }

        #endregion

        #region Add/Remove Avatar Methods

        public override ISceneAgent AddNewClient(IClientAPI client, PresenceType type)
        {
            // Validation occurs in LLUDPServer
            AgentCircuitData aCircuit = m_authenticateHandler.GetAgentCircuitData(client.CircuitCode);

            bool vialogin
                = (aCircuit.teleportFlags & (uint)Constants.TeleportFlags.ViaHGLogin) != 0
                    || (aCircuit.teleportFlags & (uint)Constants.TeleportFlags.ViaLogin) != 0;

//            CheckHeartbeat();

            ScenePresence sp = GetScenePresence(client.AgentId);

            // XXX: Not sure how good it is to add a new client if a scene presence already exists.  Possibly this
            // could occur if a viewer crashes and relogs before the old client is kicked out.  But this could cause
            // other problems, and possible the code calling AddNewClient() should ensure that no client is already
            // connected.
            if (sp == null)
            {
                m_log.DebugFormat(
                    "[SCENE]: Adding new child scene presence {0} {1} to scene {2} at pos {3}",
                    client.Name, client.AgentId, RegionInfo.RegionName, client.StartPos);

                m_clientManager.Add(client);
                SubscribeToClientEvents(client);

                sp = m_sceneGraph.CreateAndAddChildScenePresence(client, aCircuit.Appearance, type);
                m_eventManager.TriggerOnNewPresence(sp);

                sp.TeleportFlags = (TPFlags)aCircuit.teleportFlags;

                // The first agent upon login is a root agent by design.
                // For this agent we will have to rez the attachments.
                // All other AddNewClient calls find aCircuit.child to be true.
                if (aCircuit.child == false)
                {
                    // We have to set SP to be a root agent here so that SP.MakeRootAgent() will later not try to
                    // start the scripts again (since this is done in RezAttachments()).
                    // XXX: This is convoluted.
                    sp.IsChildAgent = false;

                    if (AttachmentsModule != null)
                        Util.FireAndForget(delegate(object o) { AttachmentsModule.RezAttachments(sp); });
                }
            }
            else
            {
                m_log.WarnFormat(
                    "[SCENE]: Already found {0} scene presence for {1} in {2} when asked to add new scene presence",
                    sp.IsChildAgent ? "child" : "root", sp.Name, RegionInfo.RegionName);
            }

            // We must set this here so that TriggerOnNewClient and TriggerOnClientLogin can determine whether the
            // client is for a root or child agent.
            client.SceneAgent = sp;

            m_LastLogin = Util.EnvironmentTickCount();

            // Cache the user's name
            CacheUserName(sp, aCircuit);

            EventManager.TriggerOnNewClient(client);
            if (vialogin)
                EventManager.TriggerOnClientLogin(client);

            return sp;
        }

        /// <summary>
        /// Cache the user name for later use.
        /// </summary>
        /// <param name="sp"></param>
        /// <param name="aCircuit"></param>
        private void CacheUserName(ScenePresence sp, AgentCircuitData aCircuit)
        {
            if (UserManagementModule != null)
            {
                string first = aCircuit.firstname, last = aCircuit.lastname;

                if (sp.PresenceType == PresenceType.Npc)
                {
                    UserManagementModule.AddUser(aCircuit.AgentID, first, last);
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
                if (userVerification != null && ep != null)
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
            if (aCircuit != null)
            {
                bool vialogin = false;
                if (!VerifyClient(aCircuit, ep, out vialogin))
                {
                    // if it doesn't pass, we remove the agentcircuitdata altogether
                    // and the scene presence and the client, if they exist
                    try
                    {
                        // We need to wait for the client to make UDP contact first.
                        // It's the UDP contact that creates the scene presence
                        ScenePresence sp = WaitGetScenePresence(agentID);
                        if (sp != null)
                        {
                            PresenceService.LogoutAgent(sp.ControllingClient.SessionId);

                            sp.ControllingClient.Close();
                        }
                        else
                        {
                            m_log.WarnFormat("[SCENE]: Could not find scene presence for {0}", agentID);
                        }
                        // BANG! SLASH!
                        m_authenticateHandler.RemoveCircuit(agentID);

                        return false;
                    }
                    catch (Exception e)
                    {
                        m_log.DebugFormat("[SCENE]: Exception while closing aborted client: {0}", e.StackTrace);
                    }
                }
                else
                    return true;
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
            client.OnRegionHandShakeReply += SendLayerData;
        }
        
        public virtual void SubscribeToClientPrimEvents(IClientAPI client)
        {
            client.OnUpdatePrimGroupPosition += m_sceneGraph.UpdatePrimGroupPosition;
            client.OnUpdatePrimSinglePosition += m_sceneGraph.UpdatePrimSinglePosition;

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
            client.OnGrabUpdate += m_sceneGraph.MoveObject;
            client.OnSpinStart += m_sceneGraph.SpinStart;
            client.OnSpinUpdate += m_sceneGraph.SpinObject;
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
            client.OnSetStartLocationRequest += SetHomeRezPoint;
            client.OnRegionHandleRequest += RegionHandleRequest;
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
            client.OnRegionHandShakeReply -= SendLayerData;
        }

        public virtual void UnSubscribeToClientPrimEvents(IClientAPI client)
        {
            client.OnUpdatePrimGroupPosition -= m_sceneGraph.UpdatePrimGroupPosition;
            client.OnUpdatePrimSinglePosition -= m_sceneGraph.UpdatePrimSinglePosition;

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
            client.OnGrabUpdate -= m_sceneGraph.MoveObject;
            client.OnSpinStart -= m_sceneGraph.SpinStart;
            client.OnSpinUpdate -= m_sceneGraph.SpinObject;
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
            client.OnDeGrabObject -= ProcessObjectDeGrab;
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
            client.OnUpdateInventoryItem -= UpdateInventoryItemAsset;
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
            client.OnSetStartLocationRequest -= SetHomeRezPoint;
            client.OnRegionHandleRequest -= RegionHandleRequest;
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
        public virtual void TeleportClientHome(UUID agentId, IClientAPI client)
        {
            if (EntityTransferModule != null)
            {
                EntityTransferModule.TeleportHome(agentId, client);
            }
            else
            {
                m_log.DebugFormat("[SCENE]: Unable to teleport user home: no AgentTransferModule is active");
                client.SendTeleportFailed("Unable to perform teleports on this simulator.");
            }
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
            SceneObjectGroup copy = SceneGraph.DuplicateObject(originalPrim, offset, flags, AgentID, GroupID, Quaternion.Identity);
            if (copy != null)
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

            if (target != null && target2 != null)
            {
                Vector3 direction = Vector3.Normalize(RayEnd - RayStart);
                Vector3 AXOrigin = new Vector3(RayStart.X, RayStart.Y, RayStart.Z);
                Vector3 AXdirection = new Vector3(direction.X, direction.Y, direction.Z);

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
                    SceneObjectGroup copy;
                    if (CopyRotates)
                    {
                        Quaternion worldRot = target2.GetWorldRotation();

                        // SceneObjectGroup obj = m_sceneGraph.DuplicateObject(localID, pos, target.GetEffectiveObjectFlags(), AgentID, GroupID, worldRot);
                        copy = m_sceneGraph.DuplicateObject(localID, pos, target.GetEffectiveObjectFlags(), AgentID, GroupID, worldRot);
                        //obj.Rotation = worldRot;
                        //obj.UpdateGroupRotationR(worldRot);
                    }
                    else
                    {
                        copy = m_sceneGraph.DuplicateObject(localID, pos, target.GetEffectiveObjectFlags(), AgentID, GroupID, Quaternion.Identity);
                    }
                    if (copy != null)
                        EventManager.TriggerObjectAddedToScene(copy);
                }
            }
        }

        /// <summary>
        /// Sets the Home Point.   The LoginService uses this to know where to put a user when they log-in
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="regionHandle"></param>
        /// <param name="position"></param>
        /// <param name="lookAt"></param>
        /// <param name="flags"></param>
        public virtual void SetHomeRezPoint(IClientAPI remoteClient, ulong regionHandle, Vector3 position, Vector3 lookAt, uint flags)
        {
            if (GridUserService != null && GridUserService.SetHome(remoteClient.AgentId.ToString(), RegionInfo.RegionID, position, lookAt))
                // FUBAR ALERT: this needs to be "Home position set." so the viewer saves a home-screenshot.
                m_dialogModule.SendAlertToUser(remoteClient, "Home position set.");
            else
                m_dialogModule.SendAlertToUser(remoteClient, "Set Home request Failed.");
        }

        /// <summary>
        /// Get the avatar apperance for the given client.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="appearance"></param>
        public void GetAvatarAppearance(IClientAPI client, out AvatarAppearance appearance)
        {
            AgentCircuitData aCircuit = m_authenticateHandler.GetAgentCircuitData(client.CircuitCode);

            if (aCircuit == null)
            {
                m_log.DebugFormat("[APPEARANCE] Client did not supply a circuit. Non-Linden? Creating default appearance.");
                appearance = new AvatarAppearance();
                return;
            }

            appearance = aCircuit.Appearance;
            if (appearance == null)
            {
                m_log.DebugFormat("[APPEARANCE]: Appearance not found in {0}, returning default", RegionInfo.RegionName);
                appearance = new AvatarAppearance();
            }
        }

        public override void RemoveClient(UUID agentID, bool closeChildAgents)
        {
//            CheckHeartbeat();
            bool isChildAgent = false;
            ScenePresence avatar = GetScenePresence(agentID);

            if (avatar == null)
            {
                m_log.WarnFormat(
                    "[SCENE]: Called RemoveClient() with agent ID {0} but no such presence is in the scene.", agentID);

                return;
            }

            try
            {
                isChildAgent = avatar.IsChildAgent;

                m_log.DebugFormat(
                    "[SCENE]: Removing {0} agent {1} {2} from {3}",
                    (isChildAgent ? "child" : "root"), avatar.Name, agentID, RegionInfo.RegionName);

                // Don't do this to root agents, it's not nice for the viewer
                if (closeChildAgents && isChildAgent)
                {
                    // Tell a single agent to disconnect from the region.
                    IEventQueue eq = RequestModuleInterface<IEventQueue>();
                    if (eq != null)
                    {
                        eq.DisableSimulator(RegionInfo.RegionHandle, avatar.UUID);
                    }
                    else
                    {
                        avatar.ControllingClient.SendShutdownConnectionNotice();
                    }
                }

                // Only applies to root agents.
                if (avatar.ParentID != 0)
                {
                    avatar.StandUp();
                }

                m_sceneGraph.removeUserCount(!isChildAgent);

                // TODO: We shouldn't use closeChildAgents here - it's being used by the NPC module to stop
                // unnecessary operations.  This should go away once NPCs have no accompanying IClientAPI
                if (closeChildAgents && CapsModule != null)
                    CapsModule.RemoveCaps(agentID);

                // REFACTORING PROBLEM -- well not really a problem, but just to point out that whatever
                // this method is doing is HORRIBLE!!!
                avatar.Scene.NeedSceneCacheClear(avatar.UUID);

                if (closeChildAgents && !isChildAgent)
                {
                    List<ulong> regions = avatar.KnownRegionHandles;
                    regions.Remove(RegionInfo.RegionHandle);
                    m_sceneGridService.SendCloseChildAgentConnections(agentID, regions);
                }

                m_eventManager.TriggerClientClosed(agentID, this);
                m_eventManager.TriggerOnRemovePresence(agentID);

                if (!isChildAgent)
                {
                    if (AttachmentsModule != null)
                    {
                        AttachmentsModule.DeRezAttachments(avatar);
                    }

                    ForEachClient(
                        delegate(IClientAPI client)
                        {
                            //We can safely ignore null reference exceptions.  It means the avatar is dead and cleaned up anyway
                            try { client.SendKillObject(avatar.RegionHandle, new List<uint> { avatar.LocalId }); }
                            catch (NullReferenceException) { }
                        });
                }

                // It's possible for child agents to have transactions if changes are being made cross-border.
                if (AgentTransactionsModule != null)
                    AgentTransactionsModule.RemoveAgentAssetTransactions(agentID);

                m_authenticateHandler.RemoveCircuit(avatar.ControllingClient.CircuitCode);
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
                    m_sceneGraph.RemoveScenePresence(agentID);
                    m_clientManager.Remove(agentID);
    
                    avatar.Close();
                }
                catch (Exception e)
                {
                    m_log.Error(
                        string.Format("[SCENE]: Exception in final clean up of {0} in {1}.  Exception ", avatar.Name, Name), e);
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
            if (av != null)
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
            List<uint> deleteIDs = new List<uint>();

            foreach (uint localID in localIDs)
            {
                SceneObjectPart part = GetSceneObjectPart(localID);
                if (part != null) // It is a prim
                {
                    if (part.ParentGroup != null && !part.ParentGroup.IsDeleted) // Valid
                    {
                        if (part.ParentGroup.RootPart != part) // Child part
                            continue;
                    }
                }
                deleteIDs.Add(localID);
            }
            ForEachClient(delegate(IClientAPI client) { client.SendKillObject(m_regionHandle, deleteIDs); });
        }

        #endregion

        #region RegionComms

        /// <summary>
        /// Do the work necessary to initiate a new user connection for a particular scene.
        /// At the moment, this consists of setting up the caps infrastructure
        /// The return bool should allow for connections to be refused, but as not all calling paths
        /// take proper notice of it let, we allowed banned users in still.
        /// </summary>
        /// <param name="agent">CircuitData of the agent who is connecting</param>
        /// <param name="reason">Outputs the reason for the false response on this string</param>
        /// <returns>True if the region accepts this agent.  False if it does not.  False will 
        /// also return a reason.</returns>
        public bool NewUserConnection(AgentCircuitData agent, uint teleportFlags, out string reason)
        {
            return NewUserConnection(agent, teleportFlags, out reason, true);
        }

        /// <summary>
        /// Do the work necessary to initiate a new user connection for a particular scene.
        /// At the moment, this consists of setting up the caps infrastructure
        /// The return bool should allow for connections to be refused, but as not all calling paths
        /// take proper notice of it let, we allowed banned users in still.
        /// </summary>
        /// <param name="agent">CircuitData of the agent who is connecting</param>
        /// <param name="reason">Outputs the reason for the false response on this string</param>
        /// <param name="requirePresenceLookup">True for normal presence. False for NPC
        /// or other applications where a full grid/Hypergrid presence may not be required.</param>
        /// <returns>True if the region accepts this agent.  False if it does not.  False will 
        /// also return a reason.</returns>
        public bool NewUserConnection(AgentCircuitData agent, uint teleportFlags, out string reason, bool requirePresenceLookup)
        {
            bool vialogin = ((teleportFlags & (uint)TPFlags.ViaLogin) != 0 ||
                (teleportFlags & (uint)TPFlags.ViaHGLogin) != 0);
            bool viahome = ((teleportFlags & (uint)TPFlags.ViaHome) != 0);
            bool godlike = ((teleportFlags & (uint)TPFlags.Godlike) != 0);

            reason = String.Empty;

            //Teleport flags:
            //
            // TeleportFlags.ViaGodlikeLure - Border Crossing
            // TeleportFlags.ViaLogin - Login
            // TeleportFlags.TeleportFlags.ViaLure - Teleport request sent by another user
            // TeleportFlags.ViaLandmark | TeleportFlags.ViaLocation | TeleportFlags.ViaLandmark | TeleportFlags.Default - Regular Teleport

            // Don't disable this log message - it's too helpful
            m_log.DebugFormat(
                "[SCENE]: Region {0} told of incoming {1} agent {2} {3} {4} (circuit code {5}, IP {6}, viewer {7}, teleportflags ({8}), position {9})",
                RegionInfo.RegionName,
                (agent.child ? "child" : "root"),
                agent.firstname,
                agent.lastname,
                agent.AgentID,
                agent.circuitcode,
                agent.IPAddress,
                agent.Viewer,
                ((TPFlags)teleportFlags).ToString(),
                agent.startpos
            );

            if (!LoginsEnabled)
            {
                reason = "Logins Disabled";
                return false;
            }

            //Check if the viewer is banned or in the viewer access list
            //We check if the substring is listed for higher flexebility
            bool ViewerDenied = true;

            //Check if the specific viewer is listed in the allowed viewer list
            if (m_AllowedViewers.Count > 0)
            {
                foreach (string viewer in m_AllowedViewers)
                {
                    if (viewer == agent.Viewer.Substring(0, viewer.Length).Trim().ToLower())
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
                foreach (string viewer in m_BannedViewers)
                {
                    if (viewer == agent.Viewer.Substring(0, viewer.Length).Trim().ToLower())
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
                    agent.firstname, agent.lastname, agent.Viewer);
                reason = "Access denied, your viewer is banned by the region owner";
                return false;
            }           


            ScenePresence sp = GetScenePresence(agent.AgentID);

            if (sp != null && !sp.IsChildAgent)
            {
                // We have a zombie from a crashed session. 
                // Or the same user is trying to be root twice here, won't work.
                // Kill it.
                m_log.WarnFormat(
                    "[SCENE]: Existing root scene presence detected for {0} {1} in {2} when connecting.  Removing existing presence.",
                    sp.Name, sp.UUID, RegionInfo.RegionName);

                sp.ControllingClient.Close(true);
                sp = null;
            }

            ILandObject land = LandChannel.GetLandObject(agent.startpos.X, agent.startpos.Y);

            //On login test land permisions
            if (vialogin)
            {
                if (land != null && !TestLandRestrictions(agent, land, out reason))
                {
                    return false;
                }
            }

            if (sp == null) // We don't have an [child] agent here already
            {
                if (requirePresenceLookup)
                {
                    try
                    {
                        if (!VerifyUserPresence(agent, out reason))
                            return false;
                    } catch (Exception e)
                    {
                        m_log.ErrorFormat(
                            "[SCENE]: Exception verifying presence {0}{1}", e.Message, e.StackTrace);
                        return false;
                    }
                }

                try
                {
                    if (!AuthorizeUser(agent, out reason))
                        return false;
                } catch (Exception e)
                {
                    m_log.ErrorFormat(
                        "[SCENE]: Exception authorizing user {0}{1}", e.Message, e.StackTrace);
                    return false;
                }

                m_log.InfoFormat(
                    "[SCENE]: Region {0} authenticated and authorized incoming {1} agent {2} {3} {4} (circuit code {5})",
                    RegionInfo.RegionName, (agent.child ? "child" : "root"), agent.firstname, agent.lastname,
                    agent.AgentID, agent.circuitcode);

                if (CapsModule != null)
                {
                    CapsModule.SetAgentCapsSeeds(agent);
                    CapsModule.CreateCaps(agent.AgentID);
                }
            } else
            {
                // Let the SP know how we got here. This has a lot of interesting
                // uses down the line.
                sp.TeleportFlags = (TPFlags)teleportFlags;

                if (sp.IsChildAgent)
                {
                    m_log.DebugFormat(
                        "[SCENE]: Adjusting known seeds for existing agent {0} in {1}",
                        agent.AgentID, RegionInfo.RegionName);

                    sp.AdjustKnownSeeds();
                    
                    if (CapsModule != null)
                        CapsModule.SetAgentCapsSeeds(agent);
                }
            }

            // In all cases, add or update the circuit data with the new agent circuit data and teleport flags
            agent.teleportFlags = teleportFlags;
            m_authenticateHandler.AddNewCircuit(agent.circuitcode, agent);

            if (vialogin)
            {
//                CleanDroppedAttachments();

                if (TestBorderCross(agent.startpos, Cardinals.E))
                {
                    Border crossedBorder = GetCrossedBorder(agent.startpos, Cardinals.E);
                    agent.startpos.X = crossedBorder.BorderLine.Z - 1;
                }

                if (TestBorderCross(agent.startpos, Cardinals.N))
                {
                    Border crossedBorder = GetCrossedBorder(agent.startpos, Cardinals.N);
                    agent.startpos.Y = crossedBorder.BorderLine.Z - 1;
                }

                //Mitigate http://opensimulator.org/mantis/view.php?id=3522
                // Check if start position is outside of region
                // If it is, check the Z start position also..   if not, leave it alone.
                if (BordersLocked)
                {
                    lock (EastBorders)
                    {
                        if (agent.startpos.X > EastBorders[0].BorderLine.Z)
                        {
                            m_log.Warn("FIX AGENT POSITION");
                            agent.startpos.X = EastBorders[0].BorderLine.Z * 0.5f;
                            if (agent.startpos.Z > 720)
                                agent.startpos.Z = 720;
                        }
                    }
                    lock (NorthBorders)
                    {
                        if (agent.startpos.Y > NorthBorders[0].BorderLine.Z)
                        {
                            m_log.Warn("FIX Agent POSITION");
                            agent.startpos.Y = NorthBorders[0].BorderLine.Z * 0.5f;
                            if (agent.startpos.Z > 720)
                                agent.startpos.Z = 720;
                        }
                    }
                } else
                {
                    if (agent.startpos.X > EastBorders[0].BorderLine.Z)
                    {
                        m_log.Warn("FIX AGENT POSITION");
                        agent.startpos.X = EastBorders[0].BorderLine.Z * 0.5f;
                        if (agent.startpos.Z > 720)
                            agent.startpos.Z = 720;
                    }
                    if (agent.startpos.Y > NorthBorders[0].BorderLine.Z)
                    {
                        m_log.Warn("FIX Agent POSITION");
                        agent.startpos.Y = NorthBorders[0].BorderLine.Z * 0.5f;
                        if (agent.startpos.Z > 720)
                            agent.startpos.Z = 720;
                    }
                }

                // Honor Estate teleport routing via Telehubs excluding ViaHome and GodLike TeleportFlags
                if (RegionInfo.RegionSettings.TelehubObject != UUID.Zero &&
                    RegionInfo.EstateSettings.AllowDirectTeleport == false &&
                    !viahome && !godlike)
                {
                    SceneObjectGroup telehub = GetSceneObjectGroup(RegionInfo.RegionSettings.TelehubObject);
                    // Can have multiple SpawnPoints
                    List<SpawnPoint> spawnpoints = RegionInfo.RegionSettings.SpawnPoints();
                    if (spawnpoints.Count > 1)
                    {
                        // We have multiple SpawnPoints, Route the agent to a random or sequential one
                        if (SpawnPointRouting == "random")
                            agent.startpos = spawnpoints[Util.RandomClass.Next(spawnpoints.Count) - 1].GetLocation(
                                telehub.AbsolutePosition,
                                telehub.GroupRotation
                            );
                        else
                            agent.startpos = spawnpoints[SpawnPoint()].GetLocation(
                                telehub.AbsolutePosition,
                                telehub.GroupRotation
                            );
                    }
                    else
                    {
                        // We have a single SpawnPoint and will route the agent to it
                        agent.startpos = spawnpoints[0].GetLocation(telehub.AbsolutePosition, telehub.GroupRotation);
                    }

                    return true;
                }

                // Honor parcel landing type and position.
                if (land != null)
                {
                    if (land.LandData.LandingType == (byte)1 && land.LandData.UserLocation != Vector3.Zero)
                    {
                        agent.startpos = land.LandData.UserLocation;
                    }
                }
            }

            return true;
        }

        private bool TestLandRestrictions(AgentCircuitData agent, ILandObject land,  out string reason)
        {
            bool banned = land.IsBannedFromLand(agent.AgentID);
            bool restricted = land.IsRestrictedFromLand(agent.AgentID);

            if (banned || restricted)
            {
                ILandObject nearestParcel = GetNearestAllowedParcel(agent.AgentID, agent.startpos.X, agent.startpos.Y);
                if (nearestParcel != null)
                {
                    //Move agent to nearest allowed
                    Vector3 newPosition = GetParcelCenterAtGround(nearestParcel);
                    agent.startpos.X = newPosition.X;
                    agent.startpos.Y = newPosition.Y;
                }
                else
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
            reason = String.Empty;

            IPresenceService presence = RequestModuleInterface<IPresenceService>();
            if (presence == null)
            {
                reason = String.Format("Failed to verify user presence in the grid for {0} {1} in region {2}. Presence service does not exist.", agent.firstname, agent.lastname, RegionInfo.RegionName);
                return false;
            }

            OpenSim.Services.Interfaces.PresenceInfo pinfo = presence.GetAgent(agent.SessionID);

            if (pinfo == null)
            {
                reason = String.Format("Failed to verify user presence in the grid for {0} {1}, access denied to region {2}.", agent.firstname, agent.lastname, RegionInfo.RegionName);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Verify if the user can connect to this region.  Checks the banlist and ensures that the region is set for public access
        /// </summary>
        /// <param name="agent">The circuit data for the agent</param>
        /// <param name="reason">outputs the reason to this string</param>
        /// <returns>True if the region accepts this agent.  False if it does not.  False will 
        /// also return a reason.</returns>
        protected virtual bool AuthorizeUser(AgentCircuitData agent, out string reason)
        {
            reason = String.Empty;

            if (!m_strictAccessControl) return true;
            if (Permissions.IsGod(agent.AgentID)) return true;
                      
            if (AuthorizationService != null)
            {
                if (!AuthorizationService.IsAuthorizedForRegion(
                    agent.AgentID.ToString(), agent.firstname, agent.lastname, RegionInfo.RegionID.ToString(), out reason))
                {
                    m_log.WarnFormat("[CONNECTION BEGIN]: Denied access to: {0} ({1} {2}) at {3} because {4}",
                                     agent.AgentID, agent.firstname, agent.lastname, RegionInfo.RegionName, reason);
                    
                    return false;
                }
            }

            if (RegionInfo.EstateSettings != null)
            {
                if (RegionInfo.EstateSettings.IsBanned(agent.AgentID))
                {
                    m_log.WarnFormat("[CONNECTION BEGIN]: Denied access to: {0} ({1} {2}) at {3} because the user is on the banlist",
                                     agent.AgentID, agent.firstname, agent.lastname, RegionInfo.RegionName);
                    reason = String.Format("Denied access to region {0}: You have been banned from that region.",
                                           RegionInfo.RegionName);
                    return false;
                }
            }
            else
            {
                m_log.ErrorFormat("[CONNECTION BEGIN]: Estate Settings is null!");
            }

            List<UUID> agentGroups = new List<UUID>();

            if (m_groupsModule != null)
            {
                GroupMembershipData[] GroupMembership = m_groupsModule.GetMembershipData(agent.AgentID);

                if (GroupMembership != null)
                {
                    for (int i = 0; i < GroupMembership.Length; i++)
                        agentGroups.Add(GroupMembership[i].GroupID);
                }
                else
                {
                    m_log.ErrorFormat("[CONNECTION BEGIN]: GroupMembership is null!");
                }
            }

            bool groupAccess = false;
            UUID[] estateGroups = RegionInfo.EstateSettings.EstateGroups;

            if (estateGroups != null)
            {
                foreach (UUID group in estateGroups)
                {
                    if (agentGroups.Contains(group))
                    {
                        groupAccess = true;
                        break;
                    }
                }
            }
            else
            {
                m_log.ErrorFormat("[CONNECTION BEGIN]: EstateGroups is null!");
            }

            if (!RegionInfo.EstateSettings.PublicAccess &&
                !RegionInfo.EstateSettings.HasAccess(agent.AgentID) &&
                !groupAccess)
            {
                m_log.WarnFormat("[CONNECTION BEGIN]: Denied access to: {0} ({1} {2}) at {3} because the user does not have access to the estate",
                                 agent.AgentID, agent.firstname, agent.lastname, RegionInfo.RegionName);
                reason = String.Format("Denied access to private region {0}: You are not on the access list for that region.",
                                       RegionInfo.RegionName);
                return false;
            }

            // TODO: estate/region settings are not properly hooked up
            // to ILandObject.isRestrictedFromLand()
            // if (null != LandChannel)
            // {
            //     // region seems to have local Id of 1
            //     ILandObject land = LandChannel.GetLandObject(1);
            //     if (null != land)
            //     {
            //         if (land.isBannedFromLand(agent.AgentID))
            //         {
            //             m_log.WarnFormat("[CONNECTION BEGIN]: Denied access to: {0} ({1} {2}) at {3} because the user has been banned from land",
            //                              agent.AgentID, agent.firstname, agent.lastname, RegionInfo.RegionName);
            //             reason = String.Format("Denied access to private region {0}: You are banned from that region.",
            //                                    RegionInfo.RegionName);
            //             return false;
            //         }

            //         if (land.isRestrictedFromLand(agent.AgentID))
            //         {
            //             m_log.WarnFormat("[CONNECTION BEGIN]: Denied access to: {0} ({1} {2}) at {3} because the user does not have access to the region",
            //                              agent.AgentID, agent.firstname, agent.lastname, RegionInfo.RegionName);
            //             reason = String.Format("Denied access to private region {0}: You are not on the access list for that region.",
            //                                    RegionInfo.RegionName);
            //             return false;
            //         }
            //     }
            // }

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
            return m_authenticateHandler.TryChangeCiruitCode(oldcc, newcc);
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
//            if (loggingOffUser != null)
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

        /// <summary>
        /// Triggered when an agent crosses into this sim.  Also happens on initial login.
        /// </summary>
        /// <param name="agentID"></param>
        /// <param name="position"></param>
        /// <param name="isFlying"></param>
        public virtual void AgentCrossing(UUID agentID, Vector3 position, bool isFlying)
        {
            ScenePresence presence = GetScenePresence(agentID);
            if (presence != null)
            {
                try
                {
                    presence.MakeRootAgent(position, isFlying);
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[SCENE]: Unable to do agent crossing, exception {0}{1}", e.Message, e.StackTrace);
                }
            }
            else
            {
                m_log.ErrorFormat(
                    "[SCENE]: Could not find presence for agent {0} crossing into scene {1}",
                    agentID, RegionInfo.RegionName);
            }
        }

        /// <summary>
        /// We've got an update about an agent that sees into this region, 
        /// send it to ScenePresence for processing  It's the full data.
        /// </summary>
        /// <param name="cAgentData">Agent that contains all of the relevant things about an agent.
        /// Appearance, animations, position, etc.</param>
        /// <returns>true if we handled it.</returns>
        public virtual bool IncomingChildAgentDataUpdate(AgentData cAgentData)
        {
            m_log.DebugFormat(
                "[SCENE]: Incoming child agent update for {0} in {1}", cAgentData.AgentID, RegionInfo.RegionName);

            // XPTO: if this agent is not allowed here as root, always return false

            // TODO: This check should probably be in QueryAccess().
            ILandObject nearestParcel = GetNearestAllowedParcel(cAgentData.AgentID, Constants.RegionSize / 2, Constants.RegionSize / 2);
            if (nearestParcel == null)
            {
                m_log.DebugFormat(
                    "[SCENE]: Denying root agent entry to {0} in {1}: no allowed parcel",
                    cAgentData.AgentID, RegionInfo.RegionName);

                return false;
            }

            // We have to wait until the viewer contacts this region after receiving EAC.
            // That calls AddNewClient, which finally creates the ScenePresence
            ScenePresence childAgentUpdate = WaitGetScenePresence(cAgentData.AgentID);

            if (childAgentUpdate != null)
            {
                childAgentUpdate.ChildAgentDataUpdate(cAgentData);
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
        public virtual bool IncomingChildAgentDataUpdate(AgentPosition cAgentData)
        {
            //m_log.Debug(" XXX Scene IncomingChildAgentDataUpdate POSITION in " + RegionInfo.RegionName);
            ScenePresence childAgentUpdate = GetScenePresence(cAgentData.AgentID);
            if (childAgentUpdate != null)
            {
                // I can't imagine *yet* why we would get an update if the agent is a root agent..
                // however to avoid a race condition crossing borders..
                if (childAgentUpdate.IsChildAgent)
                {
                    uint rRegionX = (uint)(cAgentData.RegionHandle >> 40);
                    uint rRegionY = (((uint)(cAgentData.RegionHandle)) >> 8);
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
        /// Poll until the requested ScenePresence appears or we timeout.
        /// </summary>
        /// <returns>The scene presence is found, else null.</returns>
        /// <param name='agentID'></param>
        protected virtual ScenePresence WaitGetScenePresence(UUID agentID)
        {
            int ntimes = 10;
            ScenePresence sp = null;
            while ((sp = GetScenePresence(agentID)) == null && (ntimes-- > 0))
                Thread.Sleep(1000);

            if (sp == null)
                m_log.WarnFormat(
                    "[SCENE PRESENCE]: Did not find presence with id {0} in {1} before timeout",
                    agentID, RegionInfo.RegionName);
//            else
//                m_log.DebugFormat(
//                    "[SCENE PRESENCE]: Found presence {0} {1} {2} in {3} after {4} waits",
//                    sp.Name, sp.UUID, sp.IsChildAgent ? "child" : "root", RegionInfo.RegionName, 10 - ntimes);

            return sp;
        }

        public virtual bool IncomingRetrieveRootAgent(UUID id, out IAgentData agent)
        {
            agent = null;
            ScenePresence sp = GetScenePresence(id);
            if ((sp != null) && (!sp.IsChildAgent))
            {
                sp.IsChildAgent = true;
                return sp.CopyAgent(out agent);
            }

            return false;
        }

        /// <summary>
        /// Tell a single agent to disconnect from the region.
        /// </summary>
        /// <param name="agentID"></param>
        /// <param name="force">
        /// Force the agent to close even if it might be in the middle of some other operation.  You do not want to
        /// force unless you are absolutely sure that the agent is dead and a normal close is not working.
        /// </param>
        public bool IncomingCloseAgent(UUID agentID, bool force)
        {
            //m_log.DebugFormat("[SCENE]: Processing incoming close agent for {0}", agentID);

            ScenePresence presence = m_sceneGraph.GetScenePresence(agentID);
            if (presence != null)
            {
                presence.ControllingClient.Close(force);
                return true;
            }

            // Agent not here
            return false;
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
            GridRegion region = GridService.GetRegionByName(RegionInfo.ScopeID, regionName);

            if (region == null)
            {
                // can't find the region: Tell viewer and abort
                remoteClient.SendTeleportFailed("The region '" + regionName + "' could not be found.");
                return;
            }

            RequestTeleportLocation(remoteClient, region.RegionHandle, position, lookat, teleportFlags);
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
            ScenePresence sp = GetScenePresence(remoteClient.AgentId);
            if (sp != null)
            {
                uint regionX = RegionInfo.RegionLocX;
                uint regionY = RegionInfo.RegionLocY;

                Utils.LongToUInts(regionHandle, out regionX, out regionY);

                int shiftx = (int) regionX - (int) RegionInfo.RegionLocX * (int)Constants.RegionSize;
                int shifty = (int) regionY - (int) RegionInfo.RegionLocY * (int)Constants.RegionSize;

                position.X += shiftx;
                position.Y += shifty;

                bool result = false;

                if (TestBorderCross(position,Cardinals.N))
                    result = true;

                if (TestBorderCross(position, Cardinals.S))
                    result = true;

                if (TestBorderCross(position, Cardinals.E))
                    result = true;

                if (TestBorderCross(position, Cardinals.W))
                    result = true;

                // bordercross if position is outside of region

                if (!result)
                {
                    regionHandle = RegionInfo.RegionHandle;
                }
                else
                {
                    // not in this region, undo the shift!
                    position.X -= shiftx;
                    position.Y -= shifty;
                }

                if (EntityTransferModule != null)
                {
                    EntityTransferModule.Teleport(sp, regionHandle, position, lookAt, teleportFlags);
                }
                else
                {
                    m_log.DebugFormat("[SCENE]: Unable to perform teleports: no AgentTransferModule is active");
                    sp.ControllingClient.SendTeleportFailed("Unable to perform teleports on this simulator.");
                }
            }
        }

        public bool CrossAgentToNewRegion(ScenePresence agent, bool isFlying)
        {
            if (EntityTransferModule != null)
            {
                return EntityTransferModule.Cross(agent, isFlying);
            }
            else
            {
                m_log.DebugFormat("[SCENE]: Unable to cross agent to neighbouring region, because there is no AgentTransferModule");
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
        /// Causes all clients to get a full object update on all of the objects in the scene.
        /// </summary>
        public void ForceClientUpdate()
        {
            EntityBase[] entityList = GetEntities();
            foreach (EntityBase ent in entityList)
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
            m_log.DebugFormat("Searching for Primitive: '{0}'", cmdparams[2]);

            EntityBase[] entityList = GetEntities();
            foreach (EntityBase ent in entityList)
            {
                if (ent is SceneObjectGroup)
                {
                    SceneObjectPart part = ((SceneObjectGroup)ent).GetPart(((SceneObjectGroup)ent).UUID);
                    if (part != null)
                    {
                        if (part.Name == cmdparams[2])
                        {
                            part.Resize(
                                new Vector3(Convert.ToSingle(cmdparams[3]), Convert.ToSingle(cmdparams[4]),
                                              Convert.ToSingle(cmdparams[5])));

                            m_log.DebugFormat("Edited scale of Primitive: {0}", part.Name);
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
            return LandChannel.GetLandObject(x, y).LandData;
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
            m_log.DebugFormat("[SCENE]: returning land for {0},{1}", x, y);
            return LandChannel.GetLandObject((int)x, (int)y).LandData;
        }

        #endregion

        #region Script Engine

        private List<ScriptEngineInterface> ScriptEngines = new List<ScriptEngineInterface>();
        public bool DumpAssetsToFile;

        /// <summary>
        ///
        /// </summary>
        /// <param name="scriptEngine"></param>
        public void AddScriptEngine(ScriptEngineInterface scriptEngine)
        {
            ScriptEngines.Add(scriptEngine);
            scriptEngine.InitializeEngine(this);
        }

        private bool ScriptDanger(SceneObjectPart part,Vector3 pos)
        {
            ILandObject parcel = LandChannel.GetLandObject(pos.X, pos.Y);
            if (part != null)
            {
                if (parcel != null)
                {
                    if ((parcel.LandData.Flags & (uint)ParcelFlags.AllowOtherScripts) != 0)
                    {
                        return true;
                    }
                    else if ((parcel.LandData.Flags & (uint)ParcelFlags.AllowGroupScripts) != 0)
                    {
                        if (part.OwnerID == parcel.LandData.OwnerID
                            || (parcel.LandData.IsGroupOwned && part.GroupID == parcel.LandData.GroupID)
                            || Permissions.IsGod(part.OwnerID))
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
                        if (part.OwnerID == parcel.LandData.OwnerID)
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

        public int GetRootAgentCount()
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
        public ScenePresence GetScenePresence(UUID agentID)
        {
            return m_sceneGraph.GetScenePresence(agentID);
        }

        /// <summary>
        /// Request the scene presence by name.
        /// </summary>
        /// <param name="firstName"></param>
        /// <param name="lastName"></param>
        /// <returns>null if the presence was not found</returns>
        public ScenePresence GetScenePresence(string firstName, string lastName)
        {
            return m_sceneGraph.GetScenePresence(firstName, lastName);
        }

        /// <summary>
        /// Request the scene presence by localID.
        /// </summary>
        /// <param name="localID"></param>
        /// <returns>null if the presence was not found</returns>
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
        /// Consider using ForEachScenePresence() or ForEachRootScenePresence() if possible since these will not
        /// involving creating a new List object.
        /// </remarks>
        /// <returns>
        /// A list of the scene presences.  Adding or removing from the list will not affect the presences in the scene.
        /// </returns>
        public List<ScenePresence> GetScenePresences()
        {
            return new List<ScenePresence>(m_sceneGraph.GetScenePresences());
        }

        /// <summary>
        /// Performs action on all avatars in the scene (root scene presences)
        /// Avatars may be an NPC or a 'real' client.
        /// </summary>
        /// <param name="action"></param>
        public void ForEachRootScenePresence(Action<ScenePresence> action)
        {
            m_sceneGraph.ForEachAvatar(action);
        }

        /// <summary>
        /// Performs action on all scene presences (root and child)
        /// </summary>
        /// <param name="action"></param>
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
        public List<SceneObjectGroup> GetSceneObjectGroups()
        {
            return m_sceneGraph.GetSceneObjectGroups();
        }

        /// <summary>
        /// Get a group via its UUID
        /// </summary>
        /// <param name="fullID"></param>
        /// <returns>null if no group with that name exists</returns>
        public SceneObjectGroup GetSceneObjectGroup(UUID fullID)
        {
            return m_sceneGraph.GetSceneObjectGroup(fullID);
        }

        /// <summary>
        /// Get a group by name from the scene (will return the first
        /// found, if there are more than one prim with the same name)
        /// </summary>
        /// <param name="name"></param>
        /// <returns>null if no group with that name exists</returns>
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
        public bool TryGetSceneObjectGroup(UUID fullID, out SceneObjectGroup sog)
        {
            sog = GetSceneObjectGroup(fullID);
            return sog != null;
        }

        /// <summary>
        /// Get a prim by name from the scene (will return the first
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

        /// <summary>
        /// Attempt to get a prim via its UUID
        /// </summary>
        /// <param name="fullID"></param>
        /// <param name="sop"></param>
        /// <returns></returns>
        public bool TryGetSceneObjectPart(UUID fullID, out SceneObjectPart sop)
        {
            sop = GetSceneObjectPart(fullID);
            return sop != null;
        }

        /// <summary>
        /// Get a scene object group that contains the prim with the given local id
        /// </summary>
        /// <param name="localID"></param>
        /// <returns>null if no scene object group containing that prim is found</returns>        
        public SceneObjectGroup GetGroupByPrim(uint localID)
        {
            return m_sceneGraph.GetGroupByPrim(localID);
        }

        /// <summary>
        /// Get a scene object group that contains the prim with the given uuid
        /// </summary>
        /// <param name="fullID"></param>
        /// <returns>null if no scene object group containing that prim is found</returns>     
        public SceneObjectGroup GetGroupByPrim(UUID fullID)
        {
            return m_sceneGraph.GetGroupByPrim(fullID);
        }

        public override bool TryGetScenePresence(UUID agentID, out ScenePresence sp)
        {
            return m_sceneGraph.TryGetScenePresence(agentID, out sp);
        }

        public bool TryGetAvatarByName(string avatarName, out ScenePresence avatar)
        {
            return m_sceneGraph.TryGetAvatarByName(avatarName, out avatar);
        }

        /// <summary>
        /// Perform an action on all clients with an avatar in this scene (root only)
        /// </summary>
        /// <param name="action"></param>
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
        public void ForEachClient(Action<IClientAPI> action)
        {
            m_clientManager.ForEachSync(action);
        }

        public bool TryGetClient(UUID avatarID, out IClientAPI client)
        {
            return m_clientManager.TryGetValue(avatarID, out client);
        }

        public bool TryGetClient(System.Net.IPEndPoint remoteEndPoint, out IClientAPI client)
        {
            return m_clientManager.TryGetValue(remoteEndPoint, out client);
        }

        public void ForEachSOG(Action<SceneObjectGroup> action)
        {
            m_sceneGraph.ForEachSOG(action);
        }

        /// <summary>
        /// Returns a list of the entities in the scene.  This is a new list so operations perform on the list itself
        /// will not affect the original list of objects in the scene.
        /// </summary>
        /// <returns></returns>
        public EntityBase[] GetEntities()
        {
            return m_sceneGraph.GetEntities();
        }

        #endregion

        public void RegionHandleRequest(IClientAPI client, UUID regionID)
        {
            ulong handle = 0;
            if (regionID == RegionInfo.RegionID)
                handle = RegionInfo.RegionHandle;
            else
            {
                GridRegion r = GridService.GetRegionByUUID(UUID.Zero, regionID);
                if (r != null)
                    handle = r.RegionHandle;
            }

            if (handle != 0)
                client.SendRegionHandle(regionID, handle);
        }

        public bool NeedSceneCacheClear(UUID agentID)
        {
            IInventoryTransferModule inv = RequestModuleInterface<IInventoryTransferModule>();
            if (inv == null)
                return true;

            return inv.NeedSceneCacheClear(agentID, this);
        }

        public void CleanTempObjects()
        {
            EntityBase[] entities = GetEntities();
            foreach (EntityBase obj in entities)
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
            SimulationDataService.RemoveObject(uuid, RegionInfo.RegionID);
        }

        public int GetHealth()
        {
            // Returns:
            // 1 = sim is up and accepting http requests. The heartbeat has
            // stopped and the sim is probably locked up, but a remote
            // admin restart may succeed
            //
            // 2 = Sim is up and the heartbeat is running. The sim is likely
            // usable for people within and logins _may_ work
            //
            // 3 = We have seen a new user enter within the past 4 minutes
            // which can be seen as positive confirmation of sim health
            //
            int health=1; // Start at 1, means we're up

            if ((Util.EnvironmentTickCountSubtract(m_lastFrameTick)) < 1000)
                health += 1;
            else
                return health;

            // A login in the last 4 mins? We can't be doing too badly
            //
            if ((Util.EnvironmentTickCountSubtract(m_LastLogin)) < 240000)
                health++;
            else
                return health;

//            CheckHeartbeat();

            return health;
        }

        // This callback allows the PhysicsScene to call back to its caller (the SceneGraph) and
        // update non-physical objects like the joint proxy objects that represent the position
        // of the joints in the scene.

        // This routine is normally called from within a lock (OdeLock) from within the OdePhysicsScene
        // WARNING: be careful of deadlocks here if you manipulate the scene. Remember you are being called
        // from within the OdePhysicsScene.

        protected internal void jointMoved(PhysicsJoint joint)
        {
            // m_parentScene.PhysicsScene.DumpJointInfo(); // non-thread-locked version; we should already be in a lock (OdeLock) when this callback is invoked
            SceneObjectPart jointProxyObject = GetSceneObjectPart(joint.ObjectNameInScene);
            if (jointProxyObject == null)
            {
                jointErrorMessage(joint, "WARNING, joint proxy not found, name " + joint.ObjectNameInScene);
                return;
            }

            // now update the joint proxy object in the scene to have the position of the joint as returned by the physics engine
            SceneObjectPart trackedBody = GetSceneObjectPart(joint.TrackedBodyName); // FIXME: causes a sequential lookup
            if (trackedBody == null) return; // the actor may have been deleted but the joint still lingers around a few frames waiting for deletion. during this time, trackedBody is NULL to prevent further motion of the joint proxy.
            jointProxyObject.Velocity = trackedBody.Velocity;
            jointProxyObject.AngularVelocity = trackedBody.AngularVelocity;
            switch (joint.Type)
            {
                case PhysicsJointType.Ball:
                    {
                        Vector3 jointAnchor = PhysicsScene.GetJointAnchor(joint);
                        Vector3 proxyPos = new Vector3(jointAnchor.X, jointAnchor.Y, jointAnchor.Z);
                        jointProxyObject.ParentGroup.UpdateGroupPosition(proxyPos); // schedules the entire group for a terse update
                    }
                    break;

                case PhysicsJointType.Hinge:
                    {
                        Vector3 jointAnchor = PhysicsScene.GetJointAnchor(joint);

                        // Normally, we would just ask the physics scene to return the axis for the joint.
                        // Unfortunately, ODE sometimes returns <0,0,0> for the joint axis, which should
                        // never occur. Therefore we cannot rely on ODE to always return a correct joint axis.
                        // Therefore the following call does not always work:
                        //PhysicsVector phyJointAxis = _PhyScene.GetJointAxis(joint);

                        // instead we compute the joint orientation by saving the original joint orientation
                        // relative to one of the jointed bodies, and applying this transformation
                        // to the current position of the jointed bodies (the tracked body) to compute the
                        // current joint orientation.

                        if (joint.TrackedBodyName == null)
                        {
                            jointErrorMessage(joint, "joint.TrackedBodyName is null, joint " + joint.ObjectNameInScene);
                        }

                        Vector3 proxyPos = new Vector3(jointAnchor.X, jointAnchor.Y, jointAnchor.Z);
                        Quaternion q = trackedBody.RotationOffset * joint.LocalRotation;

                        jointProxyObject.ParentGroup.UpdateGroupPosition(proxyPos); // schedules the entire group for a terse update
                        jointProxyObject.ParentGroup.UpdateGroupRotationR(q); // schedules the entire group for a terse update
                    }
                    break;
            }
        }

        // This callback allows the PhysicsScene to call back to its caller (the SceneGraph) and
        // update non-physical objects like the joint proxy objects that represent the position
        // of the joints in the scene.

        // This routine is normally called from within a lock (OdeLock) from within the OdePhysicsScene
        // WARNING: be careful of deadlocks here if you manipulate the scene. Remember you are being called
        // from within the OdePhysicsScene.
        protected internal void jointDeactivated(PhysicsJoint joint)
        {
            //m_log.Debug("[NINJA] SceneGraph.jointDeactivated, joint:" + joint.ObjectNameInScene);
            SceneObjectPart jointProxyObject = GetSceneObjectPart(joint.ObjectNameInScene);
            if (jointProxyObject == null)
            {
                jointErrorMessage(joint, "WARNING, trying to deactivate (stop interpolation of) joint proxy, but not found, name " + joint.ObjectNameInScene);
                return;
            }

            // turn the proxy non-physical, which also stops its client-side interpolation
            bool wasUsingPhysics = ((jointProxyObject.Flags & PrimFlags.Physics) != 0);
            if (wasUsingPhysics)
            {
                jointProxyObject.UpdatePrimFlags(false, false, true, false); // FIXME: possible deadlock here; check to make sure all the scene alterations set into motion here won't deadlock
            }
        }

        // This callback allows the PhysicsScene to call back to its caller (the SceneGraph) and
        // alert the user of errors by using the debug channel in the same way that scripts alert
        // the user of compile errors.

        // This routine is normally called from within a lock (OdeLock) from within the OdePhysicsScene
        // WARNING: be careful of deadlocks here if you manipulate the scene. Remember you are being called
        // from within the OdePhysicsScene.
        public void jointErrorMessage(PhysicsJoint joint, string message)
        {
            if (joint != null)
            {
                if (joint.ErrorMessageCount > PhysicsJoint.maxErrorMessages)
                    return;

                SceneObjectPart jointProxyObject = GetSceneObjectPart(joint.ObjectNameInScene);
                if (jointProxyObject != null)
                {
                    SimChat(Utils.StringToBytes("[NINJA]: " + message),
                        ChatTypeEnum.DebugChannel,
                        2147483647,
                        jointProxyObject.AbsolutePosition,
                        jointProxyObject.Name,
                        jointProxyObject.UUID,
                        false);

                    joint.ErrorMessageCount++;

                    if (joint.ErrorMessageCount > PhysicsJoint.maxErrorMessages)
                    {
                        SimChat(Utils.StringToBytes("[NINJA]: Too many messages for this joint, suppressing further messages."),
                            ChatTypeEnum.DebugChannel,
                            2147483647,
                            jointProxyObject.AbsolutePosition,
                            jointProxyObject.Name,
                            jointProxyObject.UUID,
                            false);
                    }
                }
                else
                {
                    // couldn't find the joint proxy object; the error message is silently suppressed
                }
            }
        }

        public Scene ConsoleScene()
        {
            if (MainConsole.Instance == null)
                return null;
            if (MainConsole.Instance.ConsoleScene is Scene)
                return (Scene)MainConsole.Instance.ConsoleScene;
            return null;
        }

        public float GetGroundHeight(float x, float y)
        {
            if (x < 0)
                x = 0;
            if (x >= Heightmap.Width)
                x = Heightmap.Width - 1;
            if (y < 0)
                y = 0;
            if (y >= Heightmap.Height)
                y = Heightmap.Height - 1;

            Vector3 p0 = new Vector3(x, y, (float)Heightmap[(int)x, (int)y]);
            Vector3 p1 = new Vector3(p0);
            Vector3 p2 = new Vector3(p0);

            p1.X += 1.0f;
            if (p1.X < Heightmap.Width)
                p1.Z = (float)Heightmap[(int)p1.X, (int)p1.Y];

            p2.Y += 1.0f;
            if (p2.Y < Heightmap.Height)
                p2.Z = (float)Heightmap[(int)p2.X, (int)p2.Y];

            Vector3 v0 = new Vector3(p1.X - p0.X, p1.Y - p0.Y, p1.Z - p0.Z);
            Vector3 v1 = new Vector3(p2.X - p0.X, p2.Y - p0.Y, p2.Z - p0.Z);

            v0.Normalize();
            v1.Normalize();

            Vector3 vsn = new Vector3();
            vsn.X = (v0.Y * v1.Z) - (v0.Z * v1.Y);
            vsn.Y = (v0.Z * v1.X) - (v0.X * v1.Z);
            vsn.Z = (v0.X * v1.Y) - (v0.Y * v1.X);
            vsn.Normalize();

            float xdiff = x - (float)((int)x);
            float ydiff = y - (float)((int)y);

            return (((vsn.X * xdiff) + (vsn.Y * ydiff)) / (-1 * vsn.Z)) + p0.Z;
        }

//        private void CheckHeartbeat()
//        {
//            if (m_firstHeartbeat)
//                return;
//
//            if (Util.EnvironmentTickCountSubtract(m_lastFrameTick) > 2000)
//                StartTimer();
//        }

        public override ISceneObject DeserializeObject(string representation)
        {
            return SceneObjectSerializer.FromXml2Format(representation);
        }

        public override bool AllowScriptCrossings
        {
            get { return m_allowScriptCrossings; }
        }

        public Vector3? GetNearestAllowedPosition(ScenePresence avatar)
        {
            ILandObject nearestParcel = GetNearestAllowedParcel(avatar.UUID, avatar.AbsolutePosition.X, avatar.AbsolutePosition.Y);

            if (nearestParcel != null)
            {
                Vector3 dir = Vector3.Normalize(Vector3.Multiply(avatar.Velocity, -1));
                //Try to get a location that feels like where they came from
                Vector3? nearestPoint = GetNearestPointInParcelAlongDirectionFromPoint(avatar.AbsolutePosition, dir, nearestParcel);
                if (nearestPoint != null)
                {
//                    m_log.DebugFormat(
//                        "[SCENE]: Found a sane previous position based on velocity for {0}, sending them to {1} in {2}",
//                        avatar.Name, nearestPoint, nearestParcel.LandData.Name);

                    return nearestPoint.Value;
                }

                //Sometimes velocity might be zero (local teleport), so try finding point along path from avatar to center of nearest parcel
                Vector3 directionToParcelCenter = Vector3.Subtract(GetParcelCenterAtGround(nearestParcel), avatar.AbsolutePosition);
                dir = Vector3.Normalize(directionToParcelCenter);
                nearestPoint = GetNearestPointInParcelAlongDirectionFromPoint(avatar.AbsolutePosition, dir, nearestParcel);
                if (nearestPoint != null)
                {
//                    m_log.DebugFormat(
//                        "[SCENE]: {0} had a zero velocity, sending them to {1}", avatar.Name, nearestPoint);

                    return nearestPoint.Value;
                }

                //Ultimate backup if we have no idea where they are
//                m_log.DebugFormat(
//                    "[SCENE]: No idea where {0} is, sending them to {1}", avatar.Name, avatar.lastKnownAllowedPosition);

                return avatar.lastKnownAllowedPosition;
            }

            //Go to the edge, this happens in teleporting to a region with no available parcels
            Vector3 nearestRegionEdgePoint = GetNearestRegionEdgePosition(avatar);

            //Debug.WriteLine("They are really in a place they don't belong, sending them to: " + nearestRegionEdgePoint.ToString());
            
            return nearestRegionEdgePoint;
        }

        private Vector3 GetParcelCenterAtGround(ILandObject parcel)
        {
            Vector2 center = GetParcelCenter(parcel);
            return GetPositionAtGround(center.X, center.Y);
        }

        private Vector3? GetNearestPointInParcelAlongDirectionFromPoint(Vector3 pos, Vector3 direction, ILandObject parcel)
        {
            Vector3 unitDirection = Vector3.Normalize(direction);
            //Making distance to search go through some sane limit of distance
            for (float distance = 0; distance < Constants.RegionSize * 2; distance += .5f)
            {
                Vector3 testPos = Vector3.Add(pos, Vector3.Multiply(unitDirection, distance));
                if (parcel.ContainsPoint((int)testPos.X, (int)testPos.Y))
                {
                    return testPos;
                }
            }
            return null;
        }

        public ILandObject GetNearestAllowedParcel(UUID avatarId, float x, float y)
        {
            List<ILandObject> all = AllParcels();
            float minParcelDistance = float.MaxValue;
            ILandObject nearestParcel = null;

            foreach (var parcel in all)
            {
                if (!parcel.IsEitherBannedOrRestricted(avatarId))
                {
                    float parcelDistance = GetParcelDistancefromPoint(parcel, x, y);
                    if (parcelDistance < minParcelDistance)
                    {
                        minParcelDistance = parcelDistance;
                        nearestParcel = parcel;
                    }
                }
            }

            return nearestParcel;
        }

        private List<ILandObject> AllParcels()
        {
            return LandChannel.AllParcels();
        }

        private float GetParcelDistancefromPoint(ILandObject parcel, float x, float y)
        {
            return Vector2.Distance(new Vector2(x, y), GetParcelCenter(parcel));
        }

        //calculate the average center point of a parcel
        private Vector2 GetParcelCenter(ILandObject parcel)
        {
            int count = 0;
            int avgx = 0;
            int avgy = 0;
            for (int x = 0; x < Constants.RegionSize; x++)
            {
                for (int y = 0; y < Constants.RegionSize; y++)
                {
                    //Just keep a running average as we check if all the points are inside or not
                    if (parcel.ContainsPoint(x, y))
                    {
                        if (count == 0)
                        {
                            avgx = x;
                            avgy = y;
                        }
                        else
                        {
                            avgx = (avgx * count + x) / (count + 1);
                            avgy = (avgy * count + y) / (count + 1);
                        }
                        count += 1;
                    }
                }
            }
            return new Vector2(avgx, avgy);
        }

        private Vector3 GetNearestRegionEdgePosition(ScenePresence avatar)
        {
            float xdistance = avatar.AbsolutePosition.X < Constants.RegionSize / 2 ? avatar.AbsolutePosition.X : Constants.RegionSize - avatar.AbsolutePosition.X;
            float ydistance = avatar.AbsolutePosition.Y < Constants.RegionSize / 2 ? avatar.AbsolutePosition.Y : Constants.RegionSize - avatar.AbsolutePosition.Y;

            //find out what vertical edge to go to
            if (xdistance < ydistance)
            {
                if (avatar.AbsolutePosition.X < Constants.RegionSize / 2)
                {
                    return GetPositionAtAvatarHeightOrGroundHeight(avatar, 0.0f, avatar.AbsolutePosition.Y);
                }
                else
                {
                    return GetPositionAtAvatarHeightOrGroundHeight(avatar, Constants.RegionSize, avatar.AbsolutePosition.Y);
                }
            }
            //find out what horizontal edge to go to
            else
            {
                if (avatar.AbsolutePosition.Y < Constants.RegionSize / 2)
                {
                    return GetPositionAtAvatarHeightOrGroundHeight(avatar, avatar.AbsolutePosition.X, 0.0f);
                }
                else
                {
                    return GetPositionAtAvatarHeightOrGroundHeight(avatar, avatar.AbsolutePosition.X, Constants.RegionSize);
                }
            }
        }

        private Vector3 GetPositionAtAvatarHeightOrGroundHeight(ScenePresence avatar, float x, float y)
        {
            Vector3 ground = GetPositionAtGround(x, y);
            if (avatar.AbsolutePosition.Z > ground.Z)
            {
                ground.Z = avatar.AbsolutePosition.Z;
            }
            return ground;
        }

        private Vector3 GetPositionAtGround(float x, float y)
        {
            return new Vector3(x, y, GetGroundHeight(x, y));
        }

        public List<UUID> GetEstateRegions(int estateID)
        {
            IEstateDataService estateDataService = EstateDataService;
            if (estateDataService == null)
                return new List<UUID>(0);

            return estateDataService.GetRegions(estateID);
        }

        public void ReloadEstateData()
        {
            IEstateDataService estateDataService = EstateDataService;
            if (estateDataService != null)
            {
                RegionInfo.EstateSettings = estateDataService.LoadEstateSettings(RegionInfo.RegionID, false);
                TriggerEstateSunUpdate();
            }
        }

        public void TriggerEstateSunUpdate()
        {
            float sun;
            if (RegionInfo.RegionSettings.UseEstateSun)
            {
                sun = (float)RegionInfo.EstateSettings.SunPosition;
                if (RegionInfo.EstateSettings.UseGlobalTime)
                {
                    sun = EventManager.GetCurrentTimeAsSunLindenHour() - 6.0f;
                }

                // 
                EventManager.TriggerEstateToolsSunUpdate(
                        RegionInfo.RegionHandle,
                        RegionInfo.EstateSettings.FixedSun,
                        RegionInfo.RegionSettings.UseEstateSun,
                        sun);
            }
            else
            {
                // Use the Sun Position from the Region Settings
                sun = (float)RegionInfo.RegionSettings.SunPosition - 6.0f;

                EventManager.TriggerEstateToolsSunUpdate(
                        RegionInfo.RegionHandle,
                        RegionInfo.RegionSettings.FixedSun,
                        RegionInfo.RegionSettings.UseEstateSun,
                        sun);
            }
        }

        private void HandleReloadEstate(string module, string[] cmd)
        {
            if (MainConsole.Instance.ConsoleScene == null ||
                (MainConsole.Instance.ConsoleScene is Scene &&
                (Scene)MainConsole.Instance.ConsoleScene == this))
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
            minX = 256;
            maxX = -256;
            minY = 256;
            maxY = -256;
            minZ = 8192;
            maxZ = -256;

            List<Vector3> offsets = new List<Vector3>();

            foreach (SceneObjectGroup g in objects)
            {
                float ominX, ominY, ominZ, omaxX, omaxY, omaxZ;

                Vector3 vec = g.AbsolutePosition;

                g.GetAxisAlignedBoundingBoxRaw(out ominX, out omaxX, out ominY, out omaxY, out ominZ, out omaxZ);
               
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
            if (mapModule != null)
                mapModule.GenerateMaptile();
        }

        private void RegenerateMaptileAndReregister(object sender, ElapsedEventArgs e)
        {
            RegenerateMaptile();

            // We need to propagate the new image UUID to the grid service
            // so that all simulators can retrieve it
            string error = GridService.RegisterRegion(RegionInfo.ScopeID, new GridRegion(RegionInfo));
            if (error != string.Empty)
                throw new Exception(error);
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
        /// <param name='agentID'></param>
        /// <param name='position'></param>
        /// <param name='reason'></param>
        /// <returns></returns>
        public bool QueryAccess(UUID agentID, Vector3 position, out string reason)
        {
            if (EntityTransferModule.IsInTransit(agentID))
            {
                reason = "Agent is still in transit from this region";

                m_log.WarnFormat(
                    "[SCENE]: Denying agent {0} entry into {1} since region still has them registered as in transit",
                    agentID, RegionInfo.RegionName);

                return false;
            }

            // FIXME: Root agent count is currently known to be inaccurate.  This forces a recount before we check.
            // However, the long term fix is to make sure root agent count is always accurate.
            m_sceneGraph.RecalculateStats();

            int num = m_sceneGraph.GetRootAgentCount();

            if (num >= RegionInfo.RegionSettings.AgentLimit)
            {
                if (!Permissions.IsAdministrator(agentID))
                {
                    reason = "The region is full";

                    m_log.DebugFormat(
                        "[SCENE]: Denying presence with id {0} entry into {1} since region is at agent limit of {2}",
                        agentID, RegionInfo.RegionName, RegionInfo.RegionSettings.AgentLimit);

                    return false;
                }
            }

            if (position == Vector3.Zero) // Teleport
            {
                if (!RegionInfo.EstateSettings.AllowDirectTeleport)
                {
                    SceneObjectGroup telehub;
                    if (RegionInfo.RegionSettings.TelehubObject != UUID.Zero && (telehub = GetSceneObjectGroup(RegionInfo.RegionSettings.TelehubObject)) != null)
                    {
                        List<SpawnPoint> spawnPoints = RegionInfo.RegionSettings.SpawnPoints();
                        bool banned = true;
                        foreach (SpawnPoint sp in spawnPoints)
                        {
                            Vector3 spawnPoint = sp.GetLocation(telehub.AbsolutePosition, telehub.GroupRotation);
                            ILandObject land = LandChannel.GetLandObject(spawnPoint.X, spawnPoint.Y);
                            if (land == null)
                                continue;
                            if (land.IsEitherBannedOrRestricted(agentID))
                                continue;
                            banned = false;
                            break;
                        }

                        if (banned)
                        {
                            reason = "No suitable landing point found";
                            return false;
                        }
                    }
                }
            }

            reason = String.Empty;
            return true;
        }

        /// <summary>
        /// This method deals with movement when an avatar is automatically moving (but this is distinct from the
        /// autopilot that moves an avatar to a sit target!.
        /// </summary>
        /// <remarks>
        /// This is not intended as a permament location for this method.
        /// </remarks>
        /// <param name="presence"></param>
        private void HandleOnSignificantClientMovement(ScenePresence presence)
        {
            if (presence.MovingToTarget)
            {
                double distanceToTarget = Util.GetDistanceTo(presence.AbsolutePosition, presence.MoveToPositionTarget);
//                            m_log.DebugFormat(
//                                "[SCENE]: Abs pos of {0} is {1}, target {2}, distance {3}",
//                                presence.Name, presence.AbsolutePosition, presence.MoveToPositionTarget, distanceToTarget);

                // Check the error term of the current position in relation to the target position
                if (distanceToTarget <= ScenePresence.SIGNIFICANT_MOVEMENT)
                {
                    // We are close enough to the target
//                        m_log.DebugFormat("[SCENEE]: Stopping autopilot of  {0}", presence.Name);

                    presence.Velocity = Vector3.Zero;
                    presence.AbsolutePosition = presence.MoveToPositionTarget;
                    presence.ResetMoveToTarget();

                    if (presence.Flying)
                    {
                        // A horrible hack to stop the avatar dead in its tracks rather than having them overshoot
                        // the target if flying.
                        // We really need to be more subtle (slow the avatar as it approaches the target) or at
                        // least be able to set collision status once, rather than 5 times to give it enough
                        // weighting so that that PhysicsActor thinks it really is colliding.
                        for (int i = 0; i < 5; i++)
                            presence.IsColliding = true;

                        if (presence.LandAtTarget)
                            presence.Flying = false;

//                            Vector3 targetPos = presence.MoveToPositionTarget;
//                            float terrainHeight = (float)presence.Scene.Heightmap[(int)targetPos.X, (int)targetPos.Y];
//                            if (targetPos.Z - terrainHeight < 0.2)
//                            {
//                                presence.Flying = false;
//                            }
                    }

//                        m_log.DebugFormat(
//                            "[SCENE]: AgentControlFlags {0}, MovementFlag {1} for {2}",
//                            presence.AgentControlFlags, presence.MovementFlag, presence.Name);
                }
                else
                {
//                        m_log.DebugFormat(
//                            "[SCENE]: Updating npc {0} at {1} for next movement to {2}",
//                            presence.Name, presence.AbsolutePosition, presence.MoveToPositionTarget);

                    Vector3 agent_control_v3 = new Vector3();
                    presence.HandleMoveToTargetUpdate(1, ref agent_control_v3);
                    presence.AddNewMovement(agent_control_v3);
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

        // Wrappers to get physics modules retrieve assets. Has to be done this way
        // because we can't assign the asset service to physics directly - at the
        // time physics are instantiated it's not registered but it will be by
        // the time the first prim exists.
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
            if (m_extraSettings == null)
                return String.Empty;

            string val;

            if (!m_extraSettings.TryGetValue(name, out val))
                return String.Empty;

            return val;
        }

        public void StoreExtraSetting(string name, string val)
        {
            string oldVal;

            if (m_extraSettings.TryGetValue(name, out oldVal))
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
            if (!m_extraSettings.ContainsKey(name))
                return;

            m_extraSettings.Remove(name);

            m_SimulationDataService.RemoveExtra(RegionInfo.RegionID, name);

            m_eventManager.TriggerExtraSettingChanged(this, name, String.Empty);
        }
    }
}
