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
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Console;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Framework.Monitoring;
using OpenSim.Region.ClientStack;
using OpenSim.Region.CoreModules.ServiceConnectorsOut.UserAccounts;
using OpenSim.Region.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Physics.Manager;
using OpenSim.Server.Base;
using OpenSim.Services.Base;
using OpenSim.Services.Interfaces;
using OpenSim.Services.UserAccountService;

namespace OpenSim
{
    /// <summary>
    /// Common OpenSimulator simulator code
    /// </summary>
    public class OpenSimBase : RegionApplicationBase
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        // These are the names of the plugin-points extended by this
        // class during system startup.
        //

        private const string PLUGIN_ASSET_CACHE = "/OpenSim/AssetCache";
        private const string PLUGIN_ASSET_SERVER_CLIENT = "/OpenSim/AssetClient";

        // OpenSim.ini Section name for ESTATES Settings
        public const string ESTATE_SECTION_NAME = "Estates";

        protected string proxyUrl;
        protected int proxyOffset = 0;
        
        public string userStatsURI = String.Empty;

        protected bool m_autoCreateClientStack = true;

        /// <value>
        /// The file used to load and save prim backup xml if no filename has been specified
        /// </value>
        protected const string DEFAULT_PRIM_BACKUP_FILENAME = "prim-backup.xml";

        public ConfigSettings ConfigurationSettings
        {
            get { return m_configSettings; }
            set { m_configSettings = value; }
        }

        protected ConfigSettings m_configSettings;

        protected ConfigurationLoader m_configLoader;

        public ConsoleCommand CreateAccount = null;

        protected List<IApplicationPlugin> m_plugins = new List<IApplicationPlugin>();

        /// <value>
        /// The config information passed into the OpenSimulator region server.
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

        protected EnvConfigSource m_EnvConfigSource = new EnvConfigSource();

        public EnvConfigSource envConfigSource
        {
            get { return m_EnvConfigSource; }
        }

        protected List<IClientNetworkServer> m_clientServers = new List<IClientNetworkServer>();
       
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

        protected IRegistryCore m_applicationRegistry = new RegistryCore();

        public IRegistryCore ApplicationRegistry
        {
            get { return m_applicationRegistry; }
        }

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
            m_config = m_configLoader.LoadConfigSettings(configSource, envConfigSource, out m_configSettings, out m_networkServersInfo);
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
            using (PluginLoader<IApplicationPlugin> loader = new PluginLoader<IApplicationPlugin>(new ApplicationPluginInitialiser(this)))
            {
                loader.Load("/OpenSim/Startup");
                m_plugins = loader.Plugins;
            }
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
            IConfig startupConfig = m_config.Source.Configs["Startup"];
            if (startupConfig != null)
            {
                string pidFile = startupConfig.GetString("PIDFile", String.Empty);
                if (pidFile != String.Empty)
                    CreatePIDFile(pidFile);
                
                userStatsURI = startupConfig.GetString("Stats_URI", String.Empty);
            }

            // Load the simulation data service
            IConfig simDataConfig = m_config.Source.Configs["SimulationDataStore"];
            if (simDataConfig == null)
                throw new Exception("Configuration file is missing the [SimulationDataStore] section.  Have you copied OpenSim.ini.example to OpenSim.ini to reference config-include/ files?");
            string module = simDataConfig.GetString("LocalServiceModule", String.Empty);
            if (String.IsNullOrEmpty(module))
                throw new Exception("Configuration file is missing the LocalServiceModule parameter in the [SimulationDataStore] section.");
            m_simulationDataService = ServerUtils.LoadPlugin<ISimulationDataService>(module, new object[] { m_config.Source });

            // Load the estate data service
            IConfig estateDataConfig = m_config.Source.Configs["EstateDataStore"];
            if (estateDataConfig == null)
                throw new Exception("Configuration file is missing the [EstateDataStore] section.  Have you copied OpenSim.ini.example to OpenSim.ini to reference config-include/ files?");
            module = estateDataConfig.GetString("LocalServiceModule", String.Empty);
            if (String.IsNullOrEmpty(module))
                throw new Exception("Configuration file is missing the LocalServiceModule parameter in the [EstateDataStore] section");
            m_estateDataService = ServerUtils.LoadPlugin<IEstateDataService>(module, new object[] { m_config.Source });

            base.StartupSpecific();

            // Create a ModuleLoader instance
            m_moduleLoader = new ModuleLoader(m_config.Source);

            LoadPlugins();
            foreach (IApplicationPlugin plugin in m_plugins)
            {
                plugin.PostInitialise();
            }

            if (m_console != null)
            {
                StatsManager.RegisterConsoleCommands(m_console);
                AddPluginCommands(m_console);
            }
        }

        protected virtual void AddPluginCommands(CommandConsole console)
        {
            List<string> topics = GetHelpTopics();

            foreach (string topic in topics)
            {
                string capitalizedTopic = char.ToUpper(topic[0]) + topic.Substring(1);

                // This is a hack to allow the user to enter the help command in upper or lowercase.  This will go
                // away at some point.
                console.Commands.AddCommand(capitalizedTopic, false, "help " + topic,
                                              "help " + capitalizedTopic,
                                              "Get help on plugin command '" + topic + "'",
                                              HandleCommanderHelp);
                console.Commands.AddCommand(capitalizedTopic, false, "help " + capitalizedTopic,
                                              "help " + capitalizedTopic,
                                              "Get help on plugin command '" + topic + "'",
                                              HandleCommanderHelp);

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
                    console.Commands.AddCommand(capitalizedTopic, false,
                                                  topic + " " + command,
                                                  topic + " " + commander.Commands[command].ShortHelp(),
                                                  String.Empty, HandleCommanderCommand);
                }
            }
        }

        private void HandleCommanderCommand(string module, string[] cmd)
        {
            SceneManager.SendCommandToPluginModules(cmd);
        }

        private void HandleCommanderHelp(string module, string[] cmd)
        {
            // Only safe for the interactive console, since it won't
            // let us come here unless both scene and commander exist
            //
            ICommander moduleCommander = SceneManager.CurrentOrFirstScene.GetCommander(cmd[1].ToLower());
            if (moduleCommander != null)
                m_console.Output(moduleCommander.Help);
        }

        protected override void Initialize()
        {
            // Called from base.StartUp()

            m_httpServerPort = m_networkServersInfo.HttpListenerPort;
            SceneManager.OnRestartSim += handleRestartRegion;

            // Only enable the watchdogs when all regions are ready.  Otherwise we get false positives when cpu is
            // heavily used during initial startup.
            //
            // FIXME: It's also possible that region ready status should be flipped during an OAR load since this
            // also makes heavy use of the CPU.
            SceneManager.OnRegionsReadyStatusChange
                += sm => { MemoryWatchdog.Enabled = sm.AllRegionsReady; Watchdog.Enabled = sm.AllRegionsReady; };
        }

        /// <summary>
        /// Execute the region creation process.  This includes setting up scene infrastructure.
        /// </summary>
        /// <param name="regionInfo"></param>
        /// <param name="portadd_flag"></param>
        /// <returns></returns>
        public IClientNetworkServer CreateRegion(RegionInfo regionInfo, bool portadd_flag, out IScene scene)
        {
            return CreateRegion(regionInfo, portadd_flag, false, out scene);
        }

        /// <summary>
        /// Execute the region creation process.  This includes setting up scene infrastructure.
        /// </summary>
        /// <param name="regionInfo"></param>
        /// <returns></returns>
        public IClientNetworkServer CreateRegion(RegionInfo regionInfo, out IScene scene)
        {
            return CreateRegion(regionInfo, false, true, out scene);
        }

        /// <summary>
        /// Execute the region creation process.  This includes setting up scene infrastructure.
        /// </summary>
        /// <param name="regionInfo"></param>
        /// <param name="portadd_flag"></param>
        /// <param name="do_post_init"></param>
        /// <returns></returns>
        public IClientNetworkServer CreateRegion(RegionInfo regionInfo, bool portadd_flag, bool do_post_init, out IScene mscene)
        {
            int port = regionInfo.InternalEndPoint.Port;

            // set initial RegionID to originRegionID in RegionInfo. (it needs for loding prims)
            // Commented this out because otherwise regions can't register with
            // the grid as there is already another region with the same UUID
            // at those coordinates. This is required for the load balancer to work.
            // --Mike, 2009.02.25
            //regionInfo.originRegionID = regionInfo.RegionID;

            // set initial ServerURI
            regionInfo.HttpPort = m_httpServerPort;
            regionInfo.ServerURI = "http://" + regionInfo.ExternalHostName + ":" + regionInfo.HttpPort.ToString() + "/";
            
            regionInfo.osSecret = m_osSecret;
            
            if ((proxyUrl.Length > 0) && (portadd_flag))
            {
                // set proxy url to RegionInfo
                regionInfo.proxyUrl = proxyUrl;
                regionInfo.ProxyOffset = proxyOffset;
                Util.XmlRpcCommand(proxyUrl, "AddPort", port, port + proxyOffset, regionInfo.ExternalHostName);
            }

            IClientNetworkServer clientServer;
            Scene scene = SetupScene(regionInfo, proxyOffset, m_config.Source, out clientServer);

            m_log.Info("[MODULES]: Loading Region's modules (old style)");

            List<IRegionModule> modules = m_moduleLoader.PickupModules(scene, ".");

            // This needs to be ahead of the script engine load, so the
            // script module can pick up events exposed by a module
            m_moduleLoader.InitialiseSharedModules(scene);

            // Use this in the future, the line above will be deprecated soon
            m_log.Info("[REGIONMODULES]: Loading Region's modules (new style)");
            IRegionModulesController controller;
            if (ApplicationRegistry.TryGet(out controller))
            {
                controller.AddRegionToModules(scene);
            }
            else m_log.Error("[REGIONMODULES]: The new RegionModulesController is missing...");

            scene.SetModuleInterfaces();

            while (regionInfo.EstateSettings.EstateOwner == UUID.Zero && MainConsole.Instance != null)
                SetUpEstateOwner(scene);

            // Prims have to be loaded after module configuration since some modules may be invoked during the load
            scene.LoadPrimsFromStorage(regionInfo.originRegionID);
            
            // TODO : Try setting resource for region xstats here on scene
            MainServer.Instance.AddStreamHandler(new RegionStatsHandler(regionInfo));
            
            scene.loadAllLandObjectsFromStorage(regionInfo.originRegionID);
            scene.EventManager.TriggerParcelPrimCountUpdate();

            try
            {
                scene.RegisterRegionWithGrid();
            }
            catch (Exception e)
            {
                m_log.ErrorFormat(
                    "[STARTUP]: Registration of region with grid failed, aborting startup due to {0} {1}", 
                    e.Message, e.StackTrace);

                // Carrying on now causes a lot of confusion down the
                // line - we need to get the user's attention
                Environment.Exit(1);
            }

            // We need to do this after we've initialized the
            // scripting engines.
            scene.CreateScriptInstances();

            SceneManager.Add(scene);

            if (m_autoCreateClientStack)
            {
                m_clientServers.Add(clientServer);
                clientServer.Start();
            }

            if (do_post_init)
            {
                foreach (IRegionModule module in modules)
                {
                    module.PostInitialise();
                }
            }
            scene.EventManager.OnShutdown += delegate() { ShutdownRegion(scene); };

            mscene = scene;

            scene.Start();
            scene.StartScripts();

            return clientServer;
        }

        /// <summary>
        /// Try to set up the estate owner for the given scene.
        /// </summary>
        /// <remarks>
        /// The involves asking the user for information about the user on the console.  If the user does not already
        /// exist then it is created.
        /// </remarks>
        /// <param name="scene"></param>
        private void SetUpEstateOwner(Scene scene)
        {
            RegionInfo regionInfo = scene.RegionInfo;

            string estateOwnerFirstName = null;
            string estateOwnerLastName = null;
            string estateOwnerEMail = null;
            string estateOwnerPassword = null;
            string rawEstateOwnerUuid = null;

            if (m_config.Source.Configs[ESTATE_SECTION_NAME] != null)
            {
                string defaultEstateOwnerName
                    = m_config.Source.Configs[ESTATE_SECTION_NAME].GetString("DefaultEstateOwnerName", "").Trim();
                string[] ownerNames = defaultEstateOwnerName.Split(' ');

                if (ownerNames.Length >= 2)
                {
                    estateOwnerFirstName = ownerNames[0];
                    estateOwnerLastName = ownerNames[1];
                }

                // Info to be used only on Standalone Mode
                rawEstateOwnerUuid = m_config.Source.Configs[ESTATE_SECTION_NAME].GetString("DefaultEstateOwnerUUID", null);
                estateOwnerEMail = m_config.Source.Configs[ESTATE_SECTION_NAME].GetString("DefaultEstateOwnerEMail", null);
                estateOwnerPassword = m_config.Source.Configs[ESTATE_SECTION_NAME].GetString("DefaultEstateOwnerPassword", null);
            }

            MainConsole.Instance.OutputFormat("Estate {0} has no owner set.", regionInfo.EstateSettings.EstateName);
            List<char> excluded = new List<char>(new char[1]{' '});


            if (estateOwnerFirstName == null || estateOwnerLastName == null)
            {
                estateOwnerFirstName = MainConsole.Instance.CmdPrompt("Estate owner first name", "Test", excluded);
                estateOwnerLastName = MainConsole.Instance.CmdPrompt("Estate owner last name", "User", excluded);
            }

            UserAccount account
                = scene.UserAccountService.GetUserAccount(regionInfo.ScopeID, estateOwnerFirstName, estateOwnerLastName);

            if (account == null)
            {

                // XXX: The LocalUserAccountServicesConnector is currently registering its inner service rather than
                // itself!
//                    if (scene.UserAccountService is LocalUserAccountServicesConnector)
//                    {
//                        IUserAccountService innerUas
//                            = ((LocalUserAccountServicesConnector)scene.UserAccountService).UserAccountService;
//
//                        m_log.DebugFormat("B {0}", innerUas.GetType());
//
//                        if (innerUas is UserAccountService)
//                        {

                if (scene.UserAccountService is UserAccountService)
                {
                    if (estateOwnerPassword == null)
                        estateOwnerPassword = MainConsole.Instance.PasswdPrompt("Password");

                    if (estateOwnerEMail == null)
                        estateOwnerEMail = MainConsole.Instance.CmdPrompt("Email");

                    if (rawEstateOwnerUuid == null)
                        rawEstateOwnerUuid = MainConsole.Instance.CmdPrompt("User ID", UUID.Random().ToString());
        
                    UUID estateOwnerUuid = UUID.Zero;
                    if (!UUID.TryParse(rawEstateOwnerUuid, out estateOwnerUuid))
                    {
                        m_log.ErrorFormat("[OPENSIM]: ID {0} is not a valid UUID", rawEstateOwnerUuid);
                        return;
                    }

                    // If we've been given a zero uuid then this signals that we should use a random user id
                    if (estateOwnerUuid == UUID.Zero)
                        estateOwnerUuid = UUID.Random();

                    account
                        = ((UserAccountService)scene.UserAccountService).CreateUser(
                            regionInfo.ScopeID,
                            estateOwnerUuid,
                            estateOwnerFirstName,
                            estateOwnerLastName,
                            estateOwnerPassword,
                            estateOwnerEMail);
                }
            }

            if (account == null)
            {
                m_log.ErrorFormat(
                    "[OPENSIM]: Unable to store account. If this simulator is connected to a grid, you must create the estate owner account first at the grid level.");
            }
            else
            {
                regionInfo.EstateSettings.EstateOwner = account.PrincipalID;
                regionInfo.EstateSettings.Save();
            }
        }

        private void ShutdownRegion(Scene scene)
        {
            m_log.DebugFormat("[SHUTDOWN]: Shutting down region {0}", scene.RegionInfo.RegionName);
            IRegionModulesController controller;
            if (ApplicationRegistry.TryGet<IRegionModulesController>(out controller))
            {
                controller.RemoveRegionFromModules(scene);
            }
        }

        public void RemoveRegion(Scene scene, bool cleanup)
        {
            // only need to check this if we are not at the
            // root level
            if ((SceneManager.CurrentScene != null) &&
                (SceneManager.CurrentScene.RegionInfo.RegionID == scene.RegionInfo.RegionID))
            {
                SceneManager.TrySetCurrentScene("..");
            }

            scene.DeleteAllSceneObjects();
            SceneManager.CloseScene(scene);
            ShutdownClientServer(scene.RegionInfo);
            
            if (!cleanup)
                return;

            if (!String.IsNullOrEmpty(scene.RegionInfo.RegionFile))
            {
                if (scene.RegionInfo.RegionFile.ToLower().EndsWith(".xml"))
                {
                    File.Delete(scene.RegionInfo.RegionFile);
                    m_log.InfoFormat("[OPENSIM]: deleting region file \"{0}\"", scene.RegionInfo.RegionFile);
                }
                if (scene.RegionInfo.RegionFile.ToLower().EndsWith(".ini"))
                {
                    try
                    {
                        IniConfigSource source = new IniConfigSource(scene.RegionInfo.RegionFile);
                        if (source.Configs[scene.RegionInfo.RegionName] != null)
                        {
                            source.Configs.Remove(scene.RegionInfo.RegionName);

                            if (source.Configs.Count == 0)
                            {
                                File.Delete(scene.RegionInfo.RegionFile);
                            }
                            else
                            {
                                source.Save(scene.RegionInfo.RegionFile);
                            }
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
            }
        }

        public void RemoveRegion(string name, bool cleanUp)
        {
            Scene target;
            if (SceneManager.TryGetScene(name, out target))
                RemoveRegion(target, cleanUp);
        }

        /// <summary>
        /// Remove a region from the simulator without deleting it permanently.
        /// </summary>
        /// <param name="scene"></param>
        /// <returns></returns>
        public void CloseRegion(Scene scene)
        {
            // only need to check this if we are not at the
            // root level
            if ((SceneManager.CurrentScene != null) &&
                (SceneManager.CurrentScene.RegionInfo.RegionID == scene.RegionInfo.RegionID))
            {
                SceneManager.TrySetCurrentScene("..");
            }

            SceneManager.CloseScene(scene);
            ShutdownClientServer(scene.RegionInfo);
        }
        
        /// <summary>
        /// Remove a region from the simulator without deleting it permanently.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public void CloseRegion(string name)
        {
            Scene target;
            if (SceneManager.TryGetScene(name, out target))
                CloseRegion(target);
        }
        
        /// <summary>
        /// Create a scene and its initial base structures.
        /// </summary>
        /// <param name="regionInfo"></param>
        /// <param name="clientServer"> </param>
        /// <returns></returns>
        protected Scene SetupScene(RegionInfo regionInfo, out IClientNetworkServer clientServer)
        {
            return SetupScene(regionInfo, 0, null, out clientServer);
        }

        /// <summary>
        /// Create a scene and its initial base structures.
        /// </summary>
        /// <param name="regionInfo"></param>
        /// <param name="proxyOffset"></param>
        /// <param name="configSource"></param>
        /// <param name="clientServer"> </param>
        /// <returns></returns>
        protected Scene SetupScene(
            RegionInfo regionInfo, int proxyOffset, IConfigSource configSource, out IClientNetworkServer clientServer)
        {
            AgentCircuitManager circuitManager = new AgentCircuitManager();
            IPAddress listenIP = regionInfo.InternalEndPoint.Address;
            //if (!IPAddress.TryParse(regionInfo.InternalEndPoint, out listenIP))
            //    listenIP = IPAddress.Parse("0.0.0.0");

            uint port = (uint) regionInfo.InternalEndPoint.Port;

            if (m_autoCreateClientStack)
            {
                clientServer
                    = m_clientStackManager.CreateServer(
                        listenIP, ref port, proxyOffset, regionInfo.m_allow_alternate_ports, configSource,
                        circuitManager);
            }
            else
            {
                clientServer = null;
            }

            regionInfo.InternalEndPoint.Port = (int) port;

            Scene scene = CreateScene(regionInfo, m_simulationDataService, m_estateDataService, circuitManager);

            if (m_autoCreateClientStack)
            {
                clientServer.AddScene(scene);
            }

            scene.LoadWorldMap();

            scene.PhysicsScene = GetPhysicsScene(scene.RegionInfo.RegionName);
            scene.PhysicsScene.RequestAssetMethod = scene.PhysicsRequestAsset;
            scene.PhysicsScene.SetTerrain(scene.Heightmap.GetFloatsSerialised());
            scene.PhysicsScene.SetWaterLevel((float) regionInfo.RegionSettings.WaterHeight);

            return scene;
        }

        protected override ClientStackManager CreateClientStackManager()
        {
            return new ClientStackManager(m_configSettings.ClientstackDll);
        }

        protected override Scene CreateScene(RegionInfo regionInfo, ISimulationDataService simDataService,
            IEstateDataService estateDataService, AgentCircuitManager circuitManager)
        {
            SceneCommunicationService sceneGridService = new SceneCommunicationService();

            return new Scene(
                regionInfo, circuitManager, sceneGridService,
                simDataService, estateDataService, m_moduleLoader, false,
                m_config.Source, m_version);
        }
        
        protected void ShutdownClientServer(RegionInfo whichRegion)
        {
            // Close and remove the clientserver for a region
            bool foundClientServer = false;
            int clientServerElement = 0;
            Location location = new Location(whichRegion.RegionHandle);

            for (int i = 0; i < m_clientServers.Count; i++)
            {
                if (m_clientServers[i].HandlesRegion(location))
                {
                    clientServerElement = i;
                    foundClientServer = true;
                    break;
                }
            }

            if (foundClientServer)
            {
                m_clientServers[clientServerElement].NetworkStop();
                m_clientServers.RemoveAt(clientServerElement);
            }
        }
        
        public void handleRestartRegion(RegionInfo whichRegion)
        {
            m_log.Info("[OPENSIM]: Got restart signal from SceneManager");

            ShutdownClientServer(whichRegion);
            IScene scene;
            CreateRegion(whichRegion, true, out scene);
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
        public class SimStatusHandler : IStreamedRequestHandler
        {
            public byte[] Handle(string path, Stream request,
                                 IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
            {
                return Util.UTF8.GetBytes("OK");
            }

            public string Name { get { return "SimStatus"; } }
            public string Description { get { return "Simulator Status"; } }

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
                get { return "/simstatus"; }
            }
        }

        /// <summary>
        /// Handler to supply the current extended status of this sim
        /// Sends the statistical data in a json serialization 
        /// </summary>
        public class XSimStatusHandler : IStreamedRequestHandler
        {
            OpenSimBase m_opensim;
            string osXStatsURI = String.Empty;

            public string Name { get { return "XSimStatus"; } }
            public string Description { get { return "Simulator XStatus"; } }
        
            public XSimStatusHandler(OpenSimBase sim)
            {
                m_opensim = sim;
                osXStatsURI = Util.SHA1Hash(sim.osSecret);
            }
            
            public byte[] Handle(string path, Stream request,
                                 IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
            {
                return Util.UTF8.GetBytes(m_opensim.StatReport(httpRequest));
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
                // This is for the OpenSimulator instance and is the osSecret hashed
                get { return "/" + osXStatsURI; }
            }
        }

        /// <summary>
        /// Handler to supply the current extended status of this sim to a user configured URI
        /// Sends the statistical data in a json serialization 
        /// If the request contains a key, "callback" the response will be wrappend in the 
        /// associated value for jsonp used with ajax/javascript
        /// </summary>
        public class UXSimStatusHandler : IStreamedRequestHandler
        {
            OpenSimBase m_opensim;
            string osUXStatsURI = String.Empty;

            public string Name { get { return "UXSimStatus"; } }
            public string Description { get { return "Simulator UXStatus"; } }
        
            public UXSimStatusHandler(OpenSimBase sim)
            {
                m_opensim = sim;
                osUXStatsURI = sim.userStatsURI;
                
            }
            
            public byte[] Handle(string path, Stream request,
                                 IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
            {
                return Util.UTF8.GetBytes(m_opensim.StatReport(httpRequest));
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
                // This is for the OpenSimulator instance and is the user provided URI 
                get { return "/" + osUXStatsURI; }
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
                SceneManager.Close();
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
            usernum = SceneManager.GetCurrentSceneAvatars().Count;
        }

        /// <summary>
        /// Get the number of regions
        /// </summary>
        /// <param name="regionnum">The first out parameter describing the number of regions</param>
        public void GetRegionNumber(out int regionnum)
        {
            regionnum = SceneManager.Scenes.Count;
        }
        
        /// <summary>
        /// Create an estate with an initial region.
        /// </summary>
        /// <remarks>
        /// This method doesn't allow an estate to be created with the same name as existing estates.
        /// </remarks>
        /// <param name="regInfo"></param>
        /// <param name="estatesByName">A list of estate names that already exist.</param>
        /// <param name="estateName">Estate name to create if already known</param>
        /// <returns>true if the estate was created, false otherwise</returns>
        public bool CreateEstate(RegionInfo regInfo, Dictionary<string, EstateSettings> estatesByName, string estateName)
        {
            // Create a new estate
            regInfo.EstateSettings = EstateDataService.LoadEstateSettings(regInfo.RegionID, true);

            string newName;
            if (estateName != null && estateName != "")
                newName = estateName;
            else
                newName = MainConsole.Instance.CmdPrompt("New estate name", regInfo.EstateSettings.EstateName);

            if (estatesByName.ContainsKey(newName))
            {
                MainConsole.Instance.OutputFormat("An estate named {0} already exists.  Please try again.", newName);
                return false;
            }
            
            regInfo.EstateSettings.EstateName = newName;
            
            // FIXME: Later on, the scene constructor will reload the estate settings no matter what.
            // Therefore, we need to do an initial save here otherwise the new estate name will be reset
            // back to the default.  The reloading of estate settings by scene could be eliminated if it
            // knows that the passed in settings in RegionInfo are already valid.  Also, it might be 
            // possible to eliminate some additional later saves made by callers of this method.
            regInfo.EstateSettings.Save();   
            
            return true;
        }
        
        /// <summary>
        /// Load the estate information for the provided RegionInfo object.
        /// </summary>
        /// <param name="regInfo"></param>
        public bool PopulateRegionEstateInfo(RegionInfo regInfo)
        {
            if (EstateDataService != null)
                regInfo.EstateSettings = EstateDataService.LoadEstateSettings(regInfo.RegionID, false);

            if (regInfo.EstateSettings.EstateID != 0)
                return false;	// estate info in the database did not change

            m_log.WarnFormat("[ESTATE] Region {0} is not part of an estate.", regInfo.RegionName);
            
            List<EstateSettings> estates = EstateDataService.LoadEstateSettingsAll();                
            Dictionary<string, EstateSettings> estatesByName = new Dictionary<string, EstateSettings>();

            foreach (EstateSettings estate in estates)
                estatesByName[estate.EstateName] = estate;

            string defaultEstateName = null;

            if (m_config.Source.Configs[ESTATE_SECTION_NAME] != null)
            {
                defaultEstateName = m_config.Source.Configs[ESTATE_SECTION_NAME].GetString("DefaultEstateName", null);

                if (defaultEstateName != null)
                {
                    EstateSettings defaultEstate;
                    bool defaultEstateJoined = false;

                    if (estatesByName.ContainsKey(defaultEstateName))
                    {
                        defaultEstate = estatesByName[defaultEstateName];

                        if (EstateDataService.LinkRegion(regInfo.RegionID, (int)defaultEstate.EstateID))
                            defaultEstateJoined = true;
                    }
                    else
                    {
                        if (CreateEstate(regInfo, estatesByName, defaultEstateName))
                            defaultEstateJoined = true;
                    }

                    if (defaultEstateJoined)
                        return true; // need to update the database
                    else
                        m_log.ErrorFormat(
                            "[OPENSIM BASE]: Joining default estate {0} failed", defaultEstateName);
                }
            }

            // If we have no default estate or creation of the default estate failed then ask the user.
            while (true)
            {
                if (estates.Count == 0)
                {
                    m_log.Info("[ESTATE]: No existing estates found.  You must create a new one.");

                    if (CreateEstate(regInfo, estatesByName, null))
                        break;
                    else
                        continue;
                }
                else
                {
                    string response
                        = MainConsole.Instance.CmdPrompt(
                            string.Format(
                                "Do you wish to join region {0} to an existing estate (yes/no)?", regInfo.RegionName),
                                "yes",
                                new List<string>() { "yes", "no" });

                    if (response == "no")
                    {
                        if (CreateEstate(regInfo, estatesByName, null))
                            break;
                        else
                            continue;
                    }
                    else
                    {
                        string[] estateNames = estatesByName.Keys.ToArray();
                        response
                            = MainConsole.Instance.CmdPrompt(
                                string.Format(
                                    "Name of estate to join.  Existing estate names are ({0})",
                                    string.Join(", ", estateNames)),
                                estateNames[0]);

                        List<int> estateIDs = EstateDataService.GetEstates(response);
                        if (estateIDs.Count < 1)
                        {
                            MainConsole.Instance.Output("The name you have entered matches no known estate.  Please try again.");
                            continue;
                        }

                        int estateID = estateIDs[0];

                        regInfo.EstateSettings = EstateDataService.LoadEstateSettings(estateID);

                        if (EstateDataService.LinkRegion(regInfo.RegionID, estateID))
                            break;

                        MainConsole.Instance.Output("Joining the estate failed. Please try again.");
                    }
                }
	    }

	    return true;	// need to update the database
	}
    }
    
    public class OpenSimConfigSource
    {
        public IConfigSource Source;

        public void Save(string path)
        {
            if (Source is IniConfigSource)
            {
                IniConfigSource iniCon = (IniConfigSource) Source;
                iniCon.Save(path);
            }
            else if (Source is XmlConfigSource)
            {
                XmlConfigSource xmlCon = (XmlConfigSource) Source;
                xmlCon.Save(path);
            }
        }
    }
}
