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
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using OpenMetaverse;
using log4net;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Console;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Statistics;
using OpenSim.Region.ClientStack;
using OpenSim.Region.Communications.Local;
using OpenSim.Region.Communications.OGS1;
using OpenSim.Region.Environment;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Physics.Manager;

namespace OpenSim
{
    /// <summary>
    /// Common OpenSim region service code
    /// </summary>
    public class OpenSimBase : RegionApplicationBase
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected string proxyUrl;
        protected int proxyOffset = 0;

        /// <summary>
        /// The file used to load and save prim backup xml if no filename has been specified
        /// </summary>
        protected const string DEFAULT_PRIM_BACKUP_FILENAME = "prim-backup.xml";

        /// <summary>
        /// The file used to load and save an opensim archive if no filename has been specified
        /// </summary>
        protected const string DEFAULT_OAR_BACKUP_FILENAME = "scene_oar.tar.gz";

        /// <summary>
        /// The file to load and save inventory if no filename has been specified
        /// </summary>
        protected const string DEFAULT_INV_BACKUP_FILENAME = "opensim_inv.tar.gz";

        protected ConfigSettings m_configSettings = new ConfigSettings();

        public ConfigSettings ConfigurationSettings
        {
            get { return m_configSettings; }
            set { m_configSettings = value; }
        }

        protected GridInfoService m_gridInfoService;

        protected List<IClientNetworkServer> m_clientServers = new List<IClientNetworkServer>();
        protected List<RegionInfo> m_regionData = new List<RegionInfo>();

        public ConsoleCommand CreateAccount = null;
        protected bool m_dumpAssetsToFile;

        protected List<IApplicationPlugin> m_plugins = new List<IApplicationPlugin>();

        /// <value>
        /// The config information passed into the OpenSim region server.
        /// </value>        
        public OpenSimConfigSource ConfigSource
        {
            get { return m_config; }
            set { m_config = value; }
        }
        protected OpenSimConfigSource m_config;

        public List<IClientNetworkServer> ClientServers
        {
            get { return m_clientServers; }
        }

        public List<RegionInfo> RegionData
        {
            get { return m_regionData; }
        }

        public new BaseHttpServer HttpServer
        {
            get { return m_httpServer; }
        }

        public uint HttpServerPort
        {
            get { return m_httpServerPort; }
        }

        protected ModuleLoader m_moduleLoader;

        public ModuleLoader ModuleLoader
        {
            get { return m_moduleLoader; }
            set { m_moduleLoader = value; }
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="configSource"></param>
        public OpenSimBase(IConfigSource configSource) : base()
        {
            LoadConfigSettings(configSource);
        }

        protected void LoadConfigSettings(IConfigSource configSource)
        {
            bool iniFileExists = false;

            IConfig startupConfig = configSource.Configs["Startup"];

            string iniFileName = startupConfig.GetString("inifile", "OpenSim.ini");
            Application.iniFilePath = Path.Combine(Util.configDir(), iniFileName);

            string masterFileName = startupConfig.GetString("inimaster", "");
            string masterfilePath = Path.Combine(Util.configDir(), masterFileName);

            m_config = new OpenSimConfigSource();
            m_config.Source = new IniConfigSource();
            m_config.Source.Merge(DefaultConfig());

            //check for .INI file (either default or name passed in command line)
            if (File.Exists(masterfilePath))
            {
                m_config.Source.Merge(new IniConfigSource(masterfilePath));
            }

            if (File.Exists(Application.iniFilePath))
            {
                iniFileExists = true;

                // From reading Nini's code, it seems that later merged keys replace earlier ones.                
                m_config.Source.Merge(new IniConfigSource(Application.iniFilePath));
            }
            else
            {
                // check for a xml config file                
                Application.iniFilePath = Path.Combine(Util.configDir(), "OpenSim.xml");

                if (File.Exists(Application.iniFilePath))
                {
                    iniFileExists = true;

                    m_config.Source = new XmlConfigSource();
                    m_config.Source.Merge(new XmlConfigSource(Application.iniFilePath));
                }
            }

            m_config.Source.Merge(configSource);

            if (!iniFileExists)
                m_config.Save("OpenSim.ini");

            ReadConfigSettings();
        }

        /// <summary>
        /// Setup a default config values in case they aren't present in the ini file
        /// </summary>
        /// <returns></returns>
        public static IConfigSource DefaultConfig()
        {
            IConfigSource defaultConfig = new IniConfigSource();
            
            {
                IConfig config = defaultConfig.Configs["Startup"];
                
                if (null == config)
                    config = defaultConfig.AddConfig("Startup");

                config.Set("gridmode", false);
                config.Set("physics", "basicphysics");
                config.Set("meshing", "ZeroMesher");
                config.Set("physical_prim", true);
                config.Set("see_into_this_sim_from_neighbor", true);
                config.Set("serverside_object_permissions", false);
                config.Set("storage_plugin", "OpenSim.Data.SQLite.dll");
                config.Set("storage_connection_string", "URI=file:OpenSim.db,version=3");                
                config.Set("storage_prim_inventories", true);
                config.Set("startup_console_commands_file", String.Empty);
                config.Set("shutdown_console_commands_file", String.Empty);
                config.Set("DefaultScriptEngine", "ScriptEngine.DotNetEngine");
                config.Set("asset_database", "sqlite");
                config.Set("clientstack_plugin", "OpenSim.Region.ClientStack.LindenUDP.dll");
            }
        
            {
                IConfig config = defaultConfig.Configs["StandAlone"];
                
                if (null == config)
                    config = defaultConfig.AddConfig("StandAlone");

                config.Set("accounts_authenticate", false);
                config.Set("welcome_message", "Welcome to OpenSimulator");
                config.Set("inventory_plugin", "OpenSim.Data.SQLite.dll");
                config.Set("inventory_source", "");
                config.Set("userDatabase_plugin", "OpenSim.Data.SQLite.dll");
                config.Set("user_source", "");
                config.Set("asset_plugin", "OpenSim.Data.SQLite.dll");
                config.Set("asset_source", "");
                config.Set("dump_assets_to_file", false);
            }
        
            {
                IConfig config = defaultConfig.Configs["Network"];
                
                if (null == config)
                    config = defaultConfig.AddConfig("Network");

                config.Set("default_location_x", 1000);
                config.Set("default_location_y", 1000);
                config.Set("http_listener_port", NetworkServersInfo.DefaultHttpListenerPort);
                config.Set("remoting_listener_port", NetworkServersInfo.RemotingListenerPort);
                config.Set("grid_server_url", "http://127.0.0.1:" + GridConfig.DefaultHttpPort.ToString());
                config.Set("grid_send_key", "null");
                config.Set("grid_recv_key", "null");
                config.Set("user_server_url", "http://127.0.0.1:" + UserConfig.DefaultHttpPort.ToString());
                config.Set("user_send_key", "null");
                config.Set("user_recv_key", "null");
                config.Set("asset_server_url", "http://127.0.0.1:" + AssetConfig.DefaultHttpPort.ToString());
                config.Set("inventory_server_url", "http://127.0.0.1:" + InventoryConfig.DefaultHttpPort.ToString());
                config.Set("secure_inventory_server", "true");
            }

            return defaultConfig;
        }

        protected virtual void ReadConfigSettings()
        {
            m_networkServersInfo = new NetworkServersInfo();

            IConfig startupConfig = m_config.Source.Configs["Startup"];

            if (startupConfig != null)
            {
                m_configSettings.Standalone = !startupConfig.GetBoolean("gridmode");
               m_configSettings.PhysicsEngine = startupConfig.GetString("physics");
               m_configSettings.MeshEngineName = startupConfig.GetString("meshing");

               m_configSettings.PhysicalPrim = startupConfig.GetBoolean("physical_prim");

                m_configSettings.See_into_region_from_neighbor = startupConfig.GetBoolean("see_into_this_sim_from_neighbor");

                m_configSettings.StorageDll = startupConfig.GetString("storage_plugin");
                if (m_configSettings.StorageDll == "OpenSim.DataStore.MonoSqlite.dll")
                {
                    m_configSettings.StorageDll = "OpenSim.Data.SQLite.dll";
                    Console.WriteLine("WARNING: OpenSim.DataStore.MonoSqlite.dll is deprecated. Set storage_plugin to OpenSim.Data.SQLite.dll.");
                    Thread.Sleep(3000);
                }

                m_configSettings.StorageConnectionString = startupConfig.GetString("storage_connection_string");
                m_configSettings.EstateConnectionString = startupConfig.GetString("estate_connection_string", m_configSettings.StorageConnectionString);
                m_configSettings.AssetStorage = startupConfig.GetString("asset_database");
                m_configSettings.ClientstackDll = startupConfig.GetString("clientstack_plugin");
            }

            IConfig standaloneConfig = m_config.Source.Configs["StandAlone"];
            if (standaloneConfig != null)
            {
                m_configSettings.StandaloneAuthenticate = standaloneConfig.GetBoolean("accounts_authenticate");
                m_configSettings.StandaloneWelcomeMessage = standaloneConfig.GetString("welcome_message");

                m_configSettings.StandaloneInventoryPlugin = standaloneConfig.GetString("inventory_plugin");
                m_configSettings.StandaloneInventorySource = standaloneConfig.GetString("inventory_source");
                m_configSettings.StandaloneUserPlugin = standaloneConfig.GetString("userDatabase_plugin");
                m_configSettings.StandaloneUserSource = standaloneConfig.GetString("user_source");
                m_configSettings.StandaloneAssetPlugin = standaloneConfig.GetString("asset_plugin");
                m_configSettings.StandaloneAssetSource = standaloneConfig.GetString("asset_source");

                m_dumpAssetsToFile = standaloneConfig.GetBoolean("dump_assets_to_file");
            }

            m_networkServersInfo.loadFromConfiguration(m_config.Source);
        }

        protected void LoadPlugins()
        {
            PluginLoader<IApplicationPlugin> loader =
                new PluginLoader<IApplicationPlugin>(new ApplicationPluginInitialiser(this));

            loader.Load("/OpenSim/Startup");
            m_plugins = loader.Plugins;
        }

        /// <summary>
        /// Performs startup specific to this region server, including initialization of the scene 
        /// such as loading configuration from disk.
        /// </summary>
        protected override void StartupSpecific()
        {
            base.StartupSpecific();
            
            m_stats = StatsManager.StartCollectingSimExtraStats();
            
            LibraryRootFolder libraryRootFolder = new LibraryRootFolder();

            // StandAlone mode? is determined by !startupConfig.GetBoolean("gridmode", false)
            if (m_configSettings.Standalone)
            {
                InitialiseStandaloneServices(libraryRootFolder);
            }
            else
            {
                // We are in grid mode
                m_commsManager 
                    = new CommunicationsOGS1(m_networkServersInfo, m_httpServer, m_assetCache, libraryRootFolder);
                
                m_httpServer.AddStreamHandler(new SimStatusHandler());
            }

            proxyUrl = ConfigSource.Source.Configs["Network"].GetString("proxy_url", "");
            proxyOffset = Int32.Parse(ConfigSource.Source.Configs["Network"].GetString("proxy_offset", "0"));

            // Create a ModuleLoader instance
            m_moduleLoader = new ModuleLoader(m_config.Source);

            LoadPlugins();
                                    
            // Only enable logins to the regions once we have completely finished starting up (apart from scripts)
            m_commsManager.GridService.RegionLoginsEnabled = true;
        }

        /// <summary>
        /// Initialises the backend services for standalone mode, and registers some http handlers
        /// </summary>
        /// <param name="libraryRootFolder"></param>
        protected void InitialiseStandaloneServices(LibraryRootFolder libraryRootFolder)
        {
            LocalInventoryService inventoryService = new LocalInventoryService();
            inventoryService.AddPlugin(m_configSettings.StandaloneInventoryPlugin, m_configSettings.StandaloneInventorySource);

            LocalUserServices userService =
                new LocalUserServices(m_networkServersInfo, m_networkServersInfo.DefaultHomeLocX,
                                      m_networkServersInfo.DefaultHomeLocY, inventoryService);
            userService.AddPlugin(m_configSettings.StandaloneUserPlugin, m_configSettings.StandaloneUserSource);

            LocalBackEndServices backendService = new LocalBackEndServices();

            LocalLoginService loginService =
                new LocalLoginService(
                    userService, m_configSettings.StandaloneWelcomeMessage, inventoryService, backendService, m_networkServersInfo,
                    m_configSettings.StandaloneAuthenticate, libraryRootFolder);

            m_commsManager
                = new CommunicationsLocal(
                    m_networkServersInfo, m_httpServer, m_assetCache, userService, userService,
                    inventoryService, backendService, backendService, userService,
                    libraryRootFolder, m_dumpAssetsToFile);

            // set up XMLRPC handler for client's initial login request message
            m_httpServer.AddXmlRPCHandler("login_to_simulator", loginService.XmlRpcLoginMethod);

            // provides the web form login
            m_httpServer.AddHTTPHandler("login", loginService.ProcessHTMLLogin);

            // Provides the LLSD login
            m_httpServer.SetDefaultLLSDHandler(loginService.LLSDLoginMethod);

            // provide grid info
            // m_gridInfoService = new GridInfoService(m_config.Source.Configs["Startup"].GetString("inifile", Path.Combine(Util.configDir(), "OpenSim.ini")));
            m_gridInfoService = new GridInfoService(m_config.Source);
            m_httpServer.AddXmlRPCHandler("get_grid_info", m_gridInfoService.XmlRpcGridInfoMethod);
            m_httpServer.AddStreamHandler(new RestStreamHandler("GET", "/get_grid_info", m_gridInfoService.RestGetGridInfoMethod));
        }

        protected override void Initialize()
        {
            //
            // Called from base.StartUp()
            //

            m_httpServerPort = m_networkServersInfo.HttpListenerPort;

            InitialiseAssetCache();

            m_sceneManager.OnRestartSim += handleRestartRegion;
        }

        /// <summary>
        /// Initialises the assetcache
        /// </summary>
        protected void InitialiseAssetCache()
        {
            IAssetServer assetServer;
            if (m_configSettings.AssetStorage == "grid")
            {
                assetServer = new GridAssetClient(m_networkServersInfo.AssetURL);
            }
            else if (m_configSettings.AssetStorage == "cryptogrid") // Decrypt-Only
            {
                assetServer = new CryptoGridAssetClient(m_networkServersInfo.AssetURL,
                                                        Environment.CurrentDirectory, true);
            }
            else if (m_configSettings.AssetStorage == "cryptogrid_eou") // Encrypts All Assets
            {
                assetServer = new CryptoGridAssetClient(m_networkServersInfo.AssetURL,
                                                        Environment.CurrentDirectory, false);
            }
            else if (m_configSettings.AssetStorage == "file")
            {
                assetServer = new FileAssetClient(m_networkServersInfo.AssetURL);
            }
            else
            {
                SQLAssetServer sqlAssetServer = new SQLAssetServer(m_configSettings.StandaloneAssetPlugin, m_configSettings.StandaloneAssetSource);
                sqlAssetServer.LoadDefaultAssets();
                assetServer = sqlAssetServer;
            }

            m_assetCache = new AssetCache(assetServer);
        }

        public UUID CreateUser(string tempfirstname, string templastname, string tempPasswd, uint regX, uint regY)
        {
            return m_commsManager.AddUser(tempfirstname, templastname, tempPasswd, regX, regY);
        }

        /// <summary>
        /// Execute the region creation process.  This includes setting up scene infrastructure.
        /// </summary>
        /// <param name="regionInfo"></param>
        /// <param name="portadd_flag"></param>
        /// <returns></returns>
        public IClientNetworkServer CreateRegion(RegionInfo regionInfo, bool portadd_flag)
        {
            return CreateRegion(regionInfo, portadd_flag, false);
        }

        /// <summary>
        /// Execute the region creation process.  This includes setting up scene infrastructure.
        /// </summary>
        /// <param name="regionInfo"></param>
        /// <param name="portadd_flag"></param>
        /// <returns></returns>
        public IClientNetworkServer CreateRegion(RegionInfo regionInfo)
        {
            return CreateRegion(regionInfo, false, true);
        }

        /// <summary>
        /// Execute the region creation process.  This includes setting up scene infrastructure.
        /// </summary>
        /// <param name="regionInfo"></param>
        /// <param name="portadd_flag"></param>
        /// <param name="do_post_init"></param>
        /// <returns></returns>
        public IClientNetworkServer CreateRegion(RegionInfo regionInfo, bool portadd_flag, bool do_post_init)
        {
            int port = regionInfo.InternalEndPoint.Port;

            // set initial originRegionID to RegionID in RegionInfo. (it needs for loding prims)
            regionInfo.originRegionID = regionInfo.RegionID;

            // set initial ServerURI
            regionInfo.ServerURI = "http://" + regionInfo.ExternalHostName + ":" + regionInfo.InternalEndPoint.Port;
            regionInfo.HttpPort = m_httpServerPort;

            if ((proxyUrl.Length > 0) && (portadd_flag))
            {
                // set proxy url to RegionInfo
                regionInfo.proxyUrl = proxyUrl;
                Util.XmlRpcCommand(proxyUrl, "AddPort", port, port + proxyOffset, regionInfo.ExternalHostName);
            }

            IClientNetworkServer clientServer;
            Scene scene = SetupScene(regionInfo, proxyOffset, m_config.Source, out clientServer);

            m_log.Info("[MODULES]: Loading Region's modules");

            List<IRegionModule> modules = m_moduleLoader.PickupModules(scene, ".");
            
            // This needs to be ahead of the script engine load, so the
            // script module can pick up events exposed by a module
            m_moduleLoader.InitialiseSharedModules(scene);

            scene.SetModuleInterfaces();
            
            // Prims have to be loaded after module configuration since some modules may be invoked during the load            
            scene.LoadPrimsFromStorage(regionInfo.originRegionID);
            
            scene.StartTimer();

            // moved these here as the terrain texture has to be created after the modules are initialized
            // and has to happen before the region is registered with the grid.
            scene.CreateTerrainTexture(false);

            try
            {
                scene.RegisterRegionWithGrid();
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[STARTUP]: Registration of region with grid failed, aborting startup - {0}", e);

                // Carrying on now causes a lot of confusion down the
                // line - we need to get the user's attention
                System.Environment.Exit(1);
            }

            // We need to do this after we've initialized the
            // scripting engines.
            scene.CreateScriptInstances();

            scene.loadAllLandObjectsFromStorage(regionInfo.originRegionID);
            scene.EventManager.TriggerParcelPrimCountUpdate();

            m_sceneManager.Add(scene);

            m_clientServers.Add(clientServer);
            m_regionData.Add(regionInfo);
            clientServer.Start();

            if (do_post_init)
            {
                foreach (IRegionModule module in modules)
                {
                    module.PostInitialise();
                }
            }

            return clientServer;
        }

        public void RemoveRegion(Scene scene, bool cleanup)
        {
            // only need to check this if we are not at the
            // root level
            if ((m_sceneManager.CurrentScene != null) &&
                (m_sceneManager.CurrentScene.RegionInfo.RegionID == scene.RegionInfo.RegionID))
            {
                m_sceneManager.TrySetCurrentScene("..");
            }
            
            scene.DeleteAllSceneObjects();
            m_regionData.Remove(scene.RegionInfo);
            m_sceneManager.CloseScene(scene);

            if (!cleanup) 
                return;

            if (!String.IsNullOrEmpty(scene.RegionInfo.RegionFile))
            {
                File.Delete(scene.RegionInfo.RegionFile);
                m_log.InfoFormat("[OPENSIM MAIN] deleting region file \"{0}\"", scene.RegionInfo.RegionFile);
            }
        }

        public void RemoveRegion(string name, bool cleanUp)
        {
            Scene target;
            if (m_sceneManager.TryGetScene(name, out target))
                RemoveRegion(target, cleanUp);
        }

        protected override StorageManager CreateStorageManager()
        {
            return CreateStorageManager(m_configSettings.StorageConnectionString, m_configSettings.EstateConnectionString);
        }

        protected StorageManager CreateStorageManager(string connectionstring, string estateconnectionstring)
        {
            return new StorageManager(m_configSettings.StorageDll, connectionstring, estateconnectionstring);
        }

        protected override ClientStackManager CreateClientStackManager()
        {
            return new ClientStackManager(m_configSettings.ClientstackDll);
        }

        protected override Scene CreateScene(RegionInfo regionInfo, StorageManager storageManager,
                                             AgentCircuitManager circuitManager)
        {
            SceneCommunicationService sceneGridService = new SceneCommunicationService(m_commsManager);
            return
                new Scene(regionInfo, circuitManager, m_commsManager, sceneGridService, m_assetCache,
                          storageManager, m_httpServer,
                          m_moduleLoader, m_dumpAssetsToFile, m_configSettings.PhysicalPrim, m_configSettings.See_into_region_from_neighbor, m_config.Source,
                          m_version);
        }

        public void handleRestartRegion(RegionInfo whichRegion)
        {
            m_log.Error("[OPENSIM MAIN]: Got restart signal from SceneManager");

            // Shutting down the client server
            bool foundClientServer = false;
            int clientServerElement = 0;

            for (int i = 0; i < m_clientServers.Count; i++)
            {
                if (m_clientServers[i].HandlesRegion(new Location(whichRegion.RegionHandle)))
                {
                    clientServerElement = i;
                    foundClientServer = true;
                    break;
                }
            }
            if (foundClientServer)
            {
                m_clientServers[clientServerElement].Server.Close();
                m_clientServers.RemoveAt(clientServerElement);
            }

            //Removing the region from the sim's database of regions..
            int RegionHandleElement = -1;
            for (int i = 0; i < m_regionData.Count; i++)
            {
                if (whichRegion.RegionHandle == m_regionData[i].RegionHandle)
                {
                    RegionHandleElement = i;
                }
            }
            if (RegionHandleElement >= 0)
            {
                m_regionData.RemoveAt(RegionHandleElement);
            }

            CreateRegion(whichRegion, true);
        }

        # region Setup methods

        protected override PhysicsScene GetPhysicsScene()
        {
            return GetPhysicsScene(m_configSettings.PhysicsEngine, m_configSettings.MeshEngineName, m_config.Source);
        }

        /// <summary>
        /// Handler to supply the current status of this sim
        ///
        /// Currently this is always OK if the simulator is still listening for connections on its HTTP service
        /// </summary>
        protected class SimStatusHandler : IStreamedRequestHandler
        {
            public byte[] Handle(string path, Stream request,
                                 OSHttpRequest httpRequest, OSHttpResponse httpResponse)
            {
                return Encoding.UTF8.GetBytes("OK");
            }

            public string ContentType
            {
                get { return "text/plain"; }
            }

            public string HttpMethod
            {
                get { return "GET"; }
            }

            public string Path
            {
                get { return "/simstatus/"; }
            }
        }

        #endregion

        /// <summary>
        /// Performs any last-minute sanity checking and shuts down the region server
        /// </summary>
        protected override void ShutdownSpecific()
        {
            if (proxyUrl.Length > 0)
            {
                Util.XmlRpcCommand(proxyUrl, "Stop");
            }

            m_log.Info("[SHUTDOWN]: Closing all threads");
            m_log.Info("[SHUTDOWN]: Killing listener thread");
            m_log.Info("[SHUTDOWN]: Killing clients");
            // TODO: implement this
            m_log.Info("[SHUTDOWN]: Closing console and terminating");

            try
            {   
                m_sceneManager.Close();
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[SHUTDOWN]: Ignoring failure during shutdown - {0}", e);
            }
        }

        /// <summary>
        /// Get the start time and up time of Region server
        /// </summary>
        /// <param name="starttime">The first out parameter describing when the Region server started</param>
        /// <param name="uptime">The second out parameter describing how long the Region server has run</param>
        public void GetRunTime(out string starttime, out string uptime)
        {
            starttime = m_startuptime.ToString();
            uptime = (DateTime.Now - m_startuptime).ToString();
        }

        /// <summary>
        /// Get the number of the avatars in the Region server
        /// </summary>
        /// <param name="usernum">The first out parameter describing the number of all the avatars in the Region server</param>
        public void GetAvatarNumber(out int usernum)
        {
            usernum = m_sceneManager.GetCurrentSceneAvatars().Count;
        }

        /// <summary>
        /// Get the number of regions
        /// </summary>
        /// <param name="regionnum">The first out parameter describing the number of regions</param>
        public void GetRegionNumber(out int regionnum)
        {
            regionnum = m_sceneManager.Scenes.Count;
        }
    }

    public class OpenSimConfigSource
    {
        public IConfigSource Source;

        public void Save(string path)
        {
            if (Source is IniConfigSource)
            {
                IniConfigSource iniCon = (IniConfigSource)Source;
                iniCon.Save(path);
            }
            else if (Source is XmlConfigSource)
            {
                XmlConfigSource xmlCon = (XmlConfigSource)Source;
                xmlCon.Save(path);
            }
        }
    }
}
