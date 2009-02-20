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
using System.Reflection;
using System.Text;
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
using OpenSim.Region.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Physics.Manager;

namespace OpenSim
{
    /// <summary>
    /// Common OpenSim simulator code
    /// </summary>
    public class OpenSimBase : RegionApplicationBase
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        // These are the names of the plugin-points extended by this
        // class during system startup.

        private const string PLUGIN_ASSET_CACHE         = "/OpenSim/AssetCache";
        private const string PLUGIN_ASSET_SERVER_CLIENT = "/OpenSim/AssetClient";

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
        
        public ConfigSettings ConfigurationSettings
        {
            get { return m_configSettings; }
            set { m_configSettings = value; }
        }
        protected ConfigSettings m_configSettings;

        protected ConfigurationLoader m_configLoader;

        protected GridInfoService m_gridInfoService;               

        public ConsoleCommand CreateAccount = null;
        
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
        protected List<IClientNetworkServer> m_clientServers = new List<IClientNetworkServer>(); 

        public new BaseHttpServer HttpServer
        {
            get { return m_httpServer; }
        }

        public uint HttpServerPort
        {
            get { return m_httpServerPort; }
        }        

        public ModuleLoader ModuleLoader
        {
            get { return m_moduleLoader; }
            set { m_moduleLoader = value; }
        }
        protected ModuleLoader m_moduleLoader;        

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="configSource"></param>
        public OpenSimBase(IConfigSource configSource) : base()
        {
            LoadConfigSettings(configSource);
        }

        protected virtual void LoadConfigSettings(IConfigSource configSource)
        {
            m_configLoader = new ConfigurationLoader();
            m_config = m_configLoader.LoadConfigSettings(configSource, out m_configSettings, out m_networkServersInfo);
            ReadExtraConfigSettings();
        }

        protected virtual void ReadExtraConfigSettings()
        {
            IConfig networkConfig = m_config.Source.Configs["Network"];
            if (networkConfig != null)
            {
                proxyUrl = networkConfig.GetString("proxy_url", "");
                proxyOffset = Int32.Parse(networkConfig.GetString("proxy_offset", "0"));
            }
        }

        protected virtual void LoadPlugins()
        {
            PluginLoader<IApplicationPlugin> loader =
                new PluginLoader<IApplicationPlugin>(new ApplicationPluginInitialiser(this));

            loader.Load("/OpenSim/Startup");
            m_plugins = loader.Plugins;
        }
        
        protected override List<string> GetHelpTopics() 
        {
            List<string> topics = base.GetHelpTopics();
            Scene s = SceneManager.CurrentOrFirstScene;
            if (s != null && s.GetCommanders() != null)
                topics.AddRange(s.GetCommanders().Keys);
            
            return topics;
        }        
        
        /// <summary>
        /// Performs startup specific to the region server, including initialization of the scene 
        /// such as loading configuration from disk.
        /// </summary>
        protected override void StartupSpecific()
        {
            base.StartupSpecific();
            
            m_stats = StatsManager.StartCollectingSimExtraStats();
            
            LibraryRootFolder libraryRootFolder = new LibraryRootFolder(m_configSettings.LibrariesXMLFile);

            // Standalone mode is determined by !startupConfig.GetBoolean("gridmode", false)
            if (m_configSettings.Standalone)
            {
                InitialiseStandaloneServices(libraryRootFolder);
            }
            else
            {
                // We are in grid mode
                InitialiseGridServices(libraryRootFolder);
            }

            // Create a ModuleLoader instance
            m_moduleLoader = new ModuleLoader(m_config.Source);

            LoadPlugins();
                                    
            // Only enable logins to the regions once we have completely finished starting up (apart from scripts)
            m_commsManager.GridService.RegionLoginsEnabled = true;

            List<string> topics = GetHelpTopics();

            foreach (string topic in topics)
            {
                m_console.Commands.AddCommand("plugin", false, "help " + topic,
                        "help " + topic,
                        "Get help on plugin command '" + topic + "'",
                        HandleCommanderHelp);

                m_console.Commands.AddCommand("plugin", false, topic,
                        topic,
                        "Execute subcommand for plugin '" + topic + "'",
                        null);

                ICommander commander = null;
                
                Scene s = SceneManager.CurrentOrFirstScene;

                if (s != null && s.GetCommanders() != null)
                {
                    if (s.GetCommanders().ContainsKey(topic))
                        commander = s.GetCommanders()[topic];
                }

                if (commander == null)
                    continue;

                foreach (string command in commander.Commands.Keys)
                {
                    m_console.Commands.AddCommand(topic, false,
                            topic + " " + command,
                            topic + " " + commander.Commands[command].ShortHelp(),
                            String.Empty, HandleCommanderCommand);
                }
            }
        }

        private void HandleCommanderCommand(string module, string[] cmd)
        {
            m_sceneManager.SendCommandToPluginModules(cmd);
        }

        private void HandleCommanderHelp(string module, string[] cmd)
        {
            // Only safe for the interactive console, since it won't
            // let us come here unless both scene and commander exist
            //
            ICommander moduleCommander = SceneManager.CurrentOrFirstScene.GetCommander(cmd[1]);
            if (moduleCommander != null)
                m_console.Notice(moduleCommander.Help);
        }

        /// <summary>
        /// Initialises the backend services for standalone mode, and registers some http handlers
        /// </summary>
        /// <param name="libraryRootFolder"></param>
        protected virtual void InitialiseStandaloneServices(LibraryRootFolder libraryRootFolder)
        {
            LocalInventoryService inventoryService = new LocalInventoryService();
            inventoryService.AddPlugin(m_configSettings.StandaloneInventoryPlugin, m_configSettings.StandaloneInventorySource);

            LocalUserServices userService =
                new LocalUserServices(
                    m_networkServersInfo.DefaultHomeLocX, m_networkServersInfo.DefaultHomeLocY, inventoryService);
            userService.AddPlugin(m_configSettings.StandaloneUserPlugin, m_configSettings.StandaloneUserSource);

            LocalBackEndServices backendService = new LocalBackEndServices();

            LocalLoginService loginService =
                new LocalLoginService(
                    userService, m_configSettings.StandaloneWelcomeMessage, inventoryService, backendService, m_networkServersInfo,
                    m_configSettings.StandaloneAuthenticate, libraryRootFolder);

            m_commsManager
                = new CommunicationsLocal(
                    m_networkServersInfo, m_httpServer, m_assetCache, userService, userService,
                    inventoryService, backendService, userService,
                    libraryRootFolder, m_configSettings.DumpAssetsToFile);

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

        protected virtual void InitialiseGridServices(LibraryRootFolder libraryRootFolder)
        {
            m_commsManager
                = new CommunicationsOGS1(m_networkServersInfo, m_httpServer, m_assetCache, libraryRootFolder);

            m_httpServer.AddStreamHandler(new SimStatusHandler());
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
        /// Initialises the asset cache. This supports legacy configuration values
        /// to ensure consistent operation, but values outside of that namespace
        /// are handled by the more generic resolution mechanism provided by 
        /// the ResolveAssetServer virtual method. If extended resolution fails, 
        /// then the normal default action is taken.
        /// Creation of the AssetCache is handled by ResolveAssetCache. This
        /// function accepts a reference to the instantiated AssetServer and
        /// returns an IAssetCache implementation, if possible. This is a virtual
        /// method.
        /// </summary>

        protected virtual void InitialiseAssetCache()
        {

            LegacyAssetClientPluginInitialiser linit = null;
            CryptoAssetClientPluginInitialiser cinit = null;
            AssetClientPluginInitialiser        init = null;

            IAssetServer assetServer = null;
            string mode = m_configSettings.AssetStorage;

           if (mode == null | mode == String.Empty)
                mode = "default";

            // If "default" is specified, then the value is adjusted
            // according to whether or not the server is running in
            // standalone mode.

            if (mode.ToLower()  == "default")
            {
                if (m_configSettings.Standalone == false)
                        mode = "grid";
                else
                        mode = "local";
            }

            switch (mode.ToLower())
            {

                // If grid is specified then the grid server is chose regardless 
                // of whether the server is standalone.

                case "grid" :
                    linit = new LegacyAssetClientPluginInitialiser(m_configSettings, m_networkServersInfo.AssetURL);
                    assetServer = loadAssetServer("Grid", linit);
                    break;


                // If cryptogrid is specified then the cryptogrid server is chose regardless 
                // of whether the server is standalone.

                case "cryptogrid" :
                    cinit = new CryptoAssetClientPluginInitialiser(m_configSettings, m_networkServersInfo.AssetURL,
                                                        Environment.CurrentDirectory, true);
                    assetServer = loadAssetServer("Crypto", cinit);
                    break;

                // If cryptogrid_eou is specified then the cryptogrid_eou server is chose regardless 
                // of whether the server is standalone.

                case "cryptogrid_eou" :
                    cinit = new CryptoAssetClientPluginInitialiser(m_configSettings, m_networkServersInfo.AssetURL,
                                                        Environment.CurrentDirectory, false);
                    assetServer = loadAssetServer("Crypto", cinit);
                    break;

                // If file is specified then the file server is chose regardless 
                // of whether the server is standalone.

                case "file" :
                    linit = new LegacyAssetClientPluginInitialiser(m_configSettings, m_networkServersInfo.AssetURL);
                    assetServer = loadAssetServer("File", linit);
                    break;

                // If local is specified then we're going to use the local SQL server
                // implementation. We drop through, because that will be the fallback
                // for the following default clause too.

                case "local" :
                    break;

                // If the asset_database value is none of the previously mentioned strings, then we
                // try to load a turnkey plugin that matches this value. If not we drop through to 
                // a local default.

                default :
                    try
                    {
                        init = new AssetClientPluginInitialiser(m_configSettings);
                        assetServer = loadAssetServer(m_configSettings.AssetStorage, init);
                        break;
                    }
                    catch {}
                    m_log.Info("[OPENSIMBASE] Default assetserver will be used");
                    break;

            }

            // Open the local SQL-based database asset server

           if (assetServer == null)
            {
                init = new AssetClientPluginInitialiser(m_configSettings);
                SQLAssetServer sqlAssetServer = (SQLAssetServer) loadAssetServer("SQL", init);
                sqlAssetServer.LoadDefaultAssets(m_configSettings.AssetSetsXMLFile);
                assetServer = sqlAssetServer;
            }

            // Initialize the asset cache, passing a reference to the selected
            // asset server interface.

            m_assetCache = ResolveAssetCache(assetServer);

        }

        // This method loads the identified asset server, passing an approrpiately
        // initialized Initialise wrapper. There should to be exactly one match,
        // if not, then the first match is used.

        private IAssetServer loadAssetServer(string id, PluginInitialiserBase pi)
        {

            if (id != null && id != String.Empty)
            {
                m_log.DebugFormat("[OPENSIMBASE] Attempting to load asset server id={0}", id);

                try
                {
                    PluginLoader<IAssetServer> loader = new PluginLoader<IAssetServer>(pi);
                    loader.AddFilter(PLUGIN_ASSET_SERVER_CLIENT, new PluginProviderFilter(id));
                    loader.Load(PLUGIN_ASSET_SERVER_CLIENT);

                    if (loader.Plugins.Count > 0)
                    {
                        m_log.DebugFormat("[OPENSIMBASE] Asset server {0} loaded", id);
                        return (IAssetServer) loader.Plugins[0];
                    }
                }
                catch (Exception e)
                {
                    m_log.DebugFormat("[OPENSIMBASE] Asset server {0} not loaded ({1})", id, e.Message);
                }
            }

            return null;

        }

        /// <summary>
        /// Attempt to instantiate an IAssetCache implementation, using the
        /// provided IAssetServer reference.
        /// An asset cache implementation must provide a constructor that
        /// accepts two parameters;
        ///   [1] A ConfigSettings reference.
        ///   [2] An IAssetServer reference.
        /// The AssetCache value is obtained from the 
        /// [StartUp]/AssetCache value in the configuration file.
        /// </summary>

        protected virtual IAssetCache ResolveAssetCache(IAssetServer assetServer)
        {

            IAssetCache assetCache = null;


            if (m_configSettings.AssetCache != null && m_configSettings.AssetCache != String.Empty)
            {

                m_log.DebugFormat("[OPENSIMBASE] Attempting to load asset cache id={0}", m_configSettings.AssetCache);

                try
                {

                    PluginInitialiserBase init = new AssetCachePluginInitialiser(m_configSettings, assetServer);
                    PluginLoader<IAssetCache> loader = new PluginLoader<IAssetCache>(init);
                    loader.AddFilter(PLUGIN_ASSET_SERVER_CLIENT, new PluginProviderFilter(m_configSettings.AssetCache));

                    loader.Load(PLUGIN_ASSET_CACHE);
                   if (loader.Plugins.Count > 0)
                        assetCache = (IAssetCache) loader.Plugins[0];
     
                }
                catch (Exception e)
                {
                    m_log.Debug("[OPENSIMBASE] ResolveAssetCache completed");
                    m_log.Debug(e);
                }
            }

            // If everything else fails, we force load the built-in asset cache

            return (IAssetCache) ((assetCache != null) ? assetCache : new AssetCache(assetServer));

        }

        public void ProcessLogin(bool LoginEnabled)
        {
            if (LoginEnabled)
            {
                m_log.Info("[Login] Login are now enabled ");
                m_commsManager.GridService.RegionLoginsEnabled = true;
            }
            else
            {
                m_log.Info("[Login] Login are now disabled ");
                m_commsManager.GridService.RegionLoginsEnabled = false;  
            }           
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
                Environment.Exit(1);
            }

            // We need to do this after we've initialized the
            // scripting engines.
            scene.CreateScriptInstances();

            scene.loadAllLandObjectsFromStorage(regionInfo.originRegionID);
            scene.EventManager.TriggerParcelPrimCountUpdate();

            m_sceneManager.Add(scene);

            m_clientServers.Add(clientServer);
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
            m_sceneManager.CloseScene(scene);

            if (!cleanup) 
                return;

            if (!String.IsNullOrEmpty(scene.RegionInfo.RegionFile))
            {
                File.Delete(scene.RegionInfo.RegionFile);
                m_log.InfoFormat("[OPENSIM]: deleting region file \"{0}\"", scene.RegionInfo.RegionFile);
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
            
            return new Scene(
                regionInfo, circuitManager, m_commsManager, sceneGridService,
                storageManager, m_moduleLoader, m_configSettings.DumpAssetsToFile, m_configSettings.PhysicalPrim, 
                m_configSettings.See_into_region_from_neighbor, m_config.Source, m_version);
        }

        public void handleRestartRegion(RegionInfo whichRegion)
        {
            m_log.Info("[OPENSIM]: Got restart signal from SceneManager");

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

            CreateRegion(whichRegion, true);
        }

        # region Setup methods

        protected override PhysicsScene GetPhysicsScene(string osSceneIdentifier)
        {
            return GetPhysicsScene(
                m_configSettings.PhysicsEngine, m_configSettings.MeshEngineName, m_config.Source, osSceneIdentifier);
        }

        /// <summary>
        /// Handler to supply the current status of this sim
        /// </summary>
        /// Currently this is always OK if the simulator is still listening for connections on its HTTP service
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
        public override void ShutdownSpecific()
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
