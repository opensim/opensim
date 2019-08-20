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
using OpenSim.Framework.Console;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Framework.Monitoring;
using OpenSim.Region.CoreModules.ServiceConnectorsOut.UserAccounts;
using OpenSim.Region.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.PhysicsModules.SharedBase;
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

        /// <summary>
        /// Allow all plugin loading to be disabled for tests/debug.
        /// </summary>
        /// <remarks>
        /// true by default
        /// </remarks>
        public bool EnableInitialPluginLoad { get; set; }

        /// <summary>
        /// Control whether we attempt to load an estate data service.
        /// </summary>
        /// <remarks>For tests/debugging</remarks>
        public bool LoadEstateDataService { get; set; }

        protected string proxyUrl;
        protected int proxyOffset = 0;

        public string userStatsURI = String.Empty;
        public string managedStatsURI = String.Empty;
        public string managedStatsPassword = String.Empty;

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

        public List<IApplicationPlugin> m_plugins = new List<IApplicationPlugin>();

        private List<string> m_permsModules;

        private bool m_securePermissionsLoading = true;

        /// <value>
        /// The config information passed into the OpenSimulator region server.
        /// </value>
        public OpenSimConfigSource ConfigSource { get; private set; }

        protected EnvConfigSource m_EnvConfigSource = new EnvConfigSource();

        public EnvConfigSource envConfigSource
        {
            get { return m_EnvConfigSource; }
        }

        public uint HttpServerPort
        {
            get { return m_httpServerPort; }
        }

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
            EnableInitialPluginLoad = true;
            LoadEstateDataService = true;
            LoadConfigSettings(configSource);
        }

        protected virtual void LoadConfigSettings(IConfigSource configSource)
        {
            m_configLoader = new ConfigurationLoader();
            ConfigSource = m_configLoader.LoadConfigSettings(configSource, envConfigSource, out m_configSettings, out m_networkServersInfo);
            Config = ConfigSource.Source;
            ReadExtraConfigSettings();
        }

        protected virtual void ReadExtraConfigSettings()
        {
            IConfig networkConfig = Config.Configs["Network"];
            if (networkConfig != null)
            {
                proxyUrl = networkConfig.GetString("proxy_url", "");
                proxyOffset = Int32.Parse(networkConfig.GetString("proxy_offset", "0"));
            }

            IConfig startupConfig = Config.Configs["Startup"];
            if (startupConfig != null)
            {
                Util.LogOverloads = startupConfig.GetBoolean("LogOverloads", true);
            }
        }

        protected virtual void LoadPlugins()
        {
            IConfig startupConfig = Config.Configs["Startup"];
            string registryLocation = (startupConfig != null) ? startupConfig.GetString("RegistryLocation", String.Empty) : String.Empty;

            // The location can also be specified in the environment. If there
            // is no location in the configuration, we must call the constructor
            // without a location parameter to allow that to happen.
            if (registryLocation == String.Empty)
            {
                using (PluginLoader<IApplicationPlugin> loader = new PluginLoader<IApplicationPlugin>(new ApplicationPluginInitialiser(this)))
                {
                    loader.Load("/OpenSim/Startup");
                    m_plugins = loader.Plugins;
                }
            }
            else
            {
                using (PluginLoader<IApplicationPlugin> loader = new PluginLoader<IApplicationPlugin>(new ApplicationPluginInitialiser(this), registryLocation))
                {
                    loader.Load("/OpenSim/Startup");
                    m_plugins = loader.Plugins;
                }
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
            IConfig startupConfig = Config.Configs["Startup"];
            if (startupConfig != null)
            {
                // refuse to run MegaRegions
                if(startupConfig.GetBoolean("CombineContiguousRegions", false))
                {
                    m_log.Fatal("CombineContiguousRegions (MegaRegions) option is no longer suported. Use a older version to save region contents as OAR, then import into a fresh install of this new version");
                    throw new Exception("CombineContiguousRegions not suported");
                }

                string pidFile = startupConfig.GetString("PIDFile", String.Empty);
                if (pidFile != String.Empty)
                    CreatePIDFile(pidFile);

                userStatsURI = startupConfig.GetString("Stats_URI", String.Empty);

                m_securePermissionsLoading = startupConfig.GetBoolean("SecurePermissionsLoading", true);

                string permissionModules = Util.GetConfigVarFromSections<string>(Config, "permissionmodules",
                    new string[] { "Startup", "Permissions" }, "DefaultPermissionsModule");

                m_permsModules =  new List<string>(permissionModules.Split(',').Select(m => m.Trim()));

                managedStatsURI = startupConfig.GetString("ManagedStatsRemoteFetchURI", String.Empty);
                managedStatsPassword = startupConfig.GetString("ManagedStatsRemoteFetchPassword", String.Empty);
            }

            // Load the simulation data service
            IConfig simDataConfig = Config.Configs["SimulationDataStore"];
            if (simDataConfig == null)
                throw new Exception("Configuration file is missing the [SimulationDataStore] section.  Have you copied OpenSim.ini.example to OpenSim.ini to reference config-include/ files?");

            string module = simDataConfig.GetString("LocalServiceModule", String.Empty);
            if (String.IsNullOrEmpty(module))
                throw new Exception("Configuration file is missing the LocalServiceModule parameter in the [SimulationDataStore] section.");

            m_simulationDataService = ServerUtils.LoadPlugin<ISimulationDataService>(module, new object[] { Config });
            if (m_simulationDataService == null)
                throw new Exception(
                    string.Format(
                        "Could not load an ISimulationDataService implementation from {0}, as configured in the LocalServiceModule parameter of the [SimulationDataStore] config section.",
                        module));

            // Load the estate data service
            module = Util.GetConfigVarFromSections<string>(Config, "LocalServiceModule", new string[]{"EstateDataStore", "EstateService"}, String.Empty);
            if (String.IsNullOrEmpty(module))
                throw new Exception("Configuration file is missing the LocalServiceModule parameter in the [EstateDataStore] or [EstateService] section");

            if (LoadEstateDataService)
            {
                m_estateDataService = ServerUtils.LoadPlugin<IEstateDataService>(module, new object[] { Config });
                if (m_estateDataService == null)
                    throw new Exception(
                        string.Format(
                            "Could not load an IEstateDataService implementation from {0}, as configured in the LocalServiceModule parameter of the [EstateDataStore] config section.",
                            module));
            }

            base.StartupSpecific();

            if (EnableInitialPluginLoad)
                LoadPlugins();

            // We still want to post initalize any plugins even if loading has been disabled since a test may have
            // inserted them manually.
            foreach (IApplicationPlugin plugin in m_plugins)
                plugin.PostInitialise();

            if (m_console != null)
                AddPluginCommands(m_console);
        }

        protected virtual void AddPluginCommands(ICommandConsole console)
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

            IConfig startupConfig = Config.Configs["Startup"];
            if (startupConfig == null || startupConfig.GetBoolean("JobEngineEnabled", true))
                WorkManager.JobEngine.Start();

           
            if(m_networkServersInfo.HttpUsesSSL)
            {
                m_httpServerSSL = true;
                m_httpServerPort = m_networkServersInfo.httpSSLPort;
            }
            else
            {
                m_httpServerSSL = false;
                m_httpServerPort = m_networkServersInfo.HttpListenerPort;
            }

            SceneManager.OnRestartSim += HandleRestartRegion;

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
        public void CreateRegion(RegionInfo regionInfo, bool portadd_flag, out IScene scene)
        {
            CreateRegion(regionInfo, portadd_flag, false, out scene);
        }

        /// <summary>
        /// Execute the region creation process.  This includes setting up scene infrastructure.
        /// </summary>
        /// <param name="regionInfo"></param>
        /// <returns></returns>
        public void CreateRegion(RegionInfo regionInfo, out IScene scene)
        {
            CreateRegion(regionInfo, false, true, out scene);
        }

        /// <summary>
        /// Execute the region creation process.  This includes setting up scene infrastructure.
        /// </summary>
        /// <param name="regionInfo"></param>
        /// <param name="portadd_flag"></param>
        /// <param name="do_post_init"></param>
        /// <returns></returns>
        public void CreateRegion(RegionInfo regionInfo, bool portadd_flag, bool do_post_init, out IScene mscene)
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
            if(m_httpServerSSL)
            {
                if(!m_httpServer.CheckSSLCertHost(regionInfo.ExternalHostName))
                    throw new Exception("main http cert CN doesn't match region External IP");

                regionInfo.ServerURI = "https://" + regionInfo.ExternalHostName +
                         ":" + regionInfo.HttpPort.ToString() + "/";
            }
            else
                regionInfo.ServerURI = "http://" + regionInfo.ExternalHostName +
                         ":" + regionInfo.HttpPort.ToString() + "/";


            regionInfo.osSecret = m_osSecret;

            if ((proxyUrl.Length > 0) && (portadd_flag))
            {
                // set proxy url to RegionInfo
                regionInfo.proxyUrl = proxyUrl;
                regionInfo.ProxyOffset = proxyOffset;
                Util.XmlRpcCommand(proxyUrl, "AddPort", port, port + proxyOffset, regionInfo.ExternalHostName);
            }

            Scene scene = SetupScene(regionInfo, proxyOffset, Config);

            m_log.Info("[MODULES]: Loading Region's modules (old style)");

            // Use this in the future, the line above will be deprecated soon
            m_log.Info("[REGIONMODULES]: Loading Region's modules (new style)");
            IRegionModulesController controller;
            if (ApplicationRegistry.TryGet(out controller))
            {
                controller.AddRegionToModules(scene);
            }
            else m_log.Error("[REGIONMODULES]: The new RegionModulesController is missing...");

            if (m_securePermissionsLoading)
            {
                foreach (string s in m_permsModules)
                {
                    if (!scene.RegionModules.ContainsKey(s))
                    {
                        m_log.Fatal("[MODULES]: Required module " + s + " not found.");
                        Environment.Exit(0);
                    }
                }

                m_log.InfoFormat("[SCENE]: Secure permissions loading enabled, modules loaded: {0}", String.Join(" ", m_permsModules.ToArray()));
            }

            scene.SetModuleInterfaces();
// First Step of bootreport sequence
            if (scene.SnmpService != null)
            {
                scene.SnmpService.ColdStart(1,scene);
                scene.SnmpService.LinkDown(scene);
            }

            if (scene.SnmpService != null)
            {
                scene.SnmpService.BootInfo("Loading prims", scene);
            }

            while (regionInfo.EstateSettings.EstateOwner == UUID.Zero && MainConsole.Instance != null)
                SetUpEstateOwner(scene);

            scene.loadAllLandObjectsFromStorage(regionInfo.originRegionID);

            // Prims have to be loaded after module configuration since some modules may be invoked during the load
            scene.LoadPrimsFromStorage(regionInfo.originRegionID);

            // TODO : Try setting resource for region xstats here on scene
            MainServer.Instance.AddStreamHandler(new RegionStatsHandler(regionInfo));

            if (scene.SnmpService != null)
            {
                scene.SnmpService.BootInfo("Grid Registration in progress", scene);
            }

            try
            {
                scene.RegisterRegionWithGrid();
            }
            catch (Exception e)
            {
                m_log.ErrorFormat(
                    "[STARTUP]: Registration of region with grid failed, aborting startup due to {0} {1}",
                    e.Message, e.StackTrace);

                if (scene.SnmpService != null)
                {
                    scene.SnmpService.Critical("Grid registration failed. Startup aborted.", scene);
                }
                // Carrying on now causes a lot of confusion down the
                // line - we need to get the user's attention
                Environment.Exit(1);
            }

            if (scene.SnmpService != null)
            {
                scene.SnmpService.BootInfo("Grid Registration done", scene);
            }

            // We need to do this after we've initialized the
            // scripting engines.
            scene.CreateScriptInstances();

            if (scene.SnmpService != null)
            {
                scene.SnmpService.BootInfo("ScriptEngine started", scene);
            }

            SceneManager.Add(scene);

            //if (m_autoCreateClientStack)
            //{
            //    foreach (IClientNetworkServer clientserver in clientServers)
            //    {
            //        m_clientServers.Add(clientserver);
            //        clientserver.Start();
            //    }
            //}

            if (scene.SnmpService != null)
            {
                scene.SnmpService.BootInfo("Initializing region modules", scene);
            }
            scene.EventManager.OnShutdown += delegate() { ShutdownRegion(scene); };

            mscene = scene;

            if (scene.SnmpService != null)
            {
                scene.SnmpService.BootInfo("The region is operational", scene);
                scene.SnmpService.LinkUp(scene);
            }

            //return clientServers;
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

            if (Config.Configs[ESTATE_SECTION_NAME] != null)
            {
                string defaultEstateOwnerName
                    = Config.Configs[ESTATE_SECTION_NAME].GetString("DefaultEstateOwnerName", "").Trim();
                string[] ownerNames = defaultEstateOwnerName.Split(' ');

                if (ownerNames.Length >= 2)
                {
                    estateOwnerFirstName = ownerNames[0];
                    estateOwnerLastName = ownerNames[1];
                }

                // Info to be used only on Standalone Mode
                rawEstateOwnerUuid = Config.Configs[ESTATE_SECTION_NAME].GetString("DefaultEstateOwnerUUID", null);
                estateOwnerEMail = Config.Configs[ESTATE_SECTION_NAME].GetString("DefaultEstateOwnerEMail", null);
                estateOwnerPassword = Config.Configs[ESTATE_SECTION_NAME].GetString("DefaultEstateOwnerPassword", null);
            }

            MainConsole.Instance.Output("Estate {0} has no owner set.", null, regionInfo.EstateSettings.EstateName);
            List<char> excluded = new List<char>(new char[1]{' '});


            if (estateOwnerFirstName == null || estateOwnerLastName == null)
            {
                estateOwnerFirstName = MainConsole.Instance.Prompt("Estate owner first name", "Test", excluded);
                estateOwnerLastName = MainConsole.Instance.Prompt("Estate owner last name", "User", excluded);
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
                        estateOwnerPassword = MainConsole.Instance.Prompt("Password", null, null, false);

                    if (estateOwnerEMail == null)
                        estateOwnerEMail = MainConsole.Instance.Prompt("Email");

                    if (rawEstateOwnerUuid == null)
                        rawEstateOwnerUuid = MainConsole.Instance.Prompt("User ID", UUID.Random().ToString());

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
                m_estateDataService.StoreEstateSettings(regionInfo.EstateSettings);
            }
        }

        private void ShutdownRegion(Scene scene)
        {
            m_log.DebugFormat("[SHUTDOWN]: Shutting down region {0}", scene.RegionInfo.RegionName);
            if (scene.SnmpService != null)
            {
                scene.SnmpService.BootInfo("The region is shutting down", scene);
                scene.SnmpService.LinkDown(scene);
            }
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
            //ShutdownClientServer(scene.RegionInfo);

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
            //ShutdownClientServer(scene.RegionInfo);
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
        protected Scene SetupScene(RegionInfo regionInfo)
        {
            return SetupScene(regionInfo, 0, null);
        }

        /// <summary>
        /// Create a scene and its initial base structures.
        /// </summary>
        /// <param name="regionInfo"></param>
        /// <param name="proxyOffset"></param>
        /// <param name="configSource"></param>
        /// <param name="clientServer"> </param>
        /// <returns></returns>
        protected Scene SetupScene(RegionInfo regionInfo, int proxyOffset, IConfigSource configSource)
        {
            //List<IClientNetworkServer> clientNetworkServers = null;

            AgentCircuitManager circuitManager = new AgentCircuitManager();
            Scene scene = CreateScene(regionInfo, m_simulationDataService, m_estateDataService, circuitManager);

            scene.LoadWorldMap();

            return scene;
        }

        protected override Scene CreateScene(RegionInfo regionInfo, ISimulationDataService simDataService,
            IEstateDataService estateDataService, AgentCircuitManager circuitManager)
        {
            return new Scene(
                regionInfo, circuitManager,
                simDataService, estateDataService,
                Config, m_version);
        }

        protected virtual void HandleRestartRegion(RegionInfo whichRegion)
        {
            m_log.InfoFormat(
                "[OPENSIM]: Got restart signal from SceneManager for region {0} ({1},{2})",
                whichRegion.RegionName, whichRegion.RegionLocX, whichRegion.RegionLocY);

            //ShutdownClientServer(whichRegion);
            IScene scene;
            CreateRegion(whichRegion, true, out scene);
            scene.Start();
        }

        # region Setup methods

        /// <summary>
        /// Handler to supply the current status of this sim
        /// </summary>
        /// <remarks>
        /// Currently this is always OK if the simulator is still listening for connections on its HTTP service
        /// </remarks>
        public class SimStatusHandler : BaseStreamHandler
        {
            public SimStatusHandler() : base("GET", "/simstatus", "SimStatus", "Simulator Status") {}

            protected override byte[] ProcessRequest(string path, Stream request,
                                 IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
            {
                return Util.UTF8.GetBytes("OK");
            }

            public override string ContentType
            {
                get { return "text/plain"; }
            }
        }

        /// <summary>
        /// Handler to supply the current extended status of this sim
        /// Sends the statistical data in a json serialization
        /// </summary>
        public class XSimStatusHandler : BaseStreamHandler
        {
            OpenSimBase m_opensim;

            public XSimStatusHandler(OpenSimBase sim)
                : base("GET", "/" + Util.SHA1Hash(sim.osSecret), "XSimStatus", "Simulator XStatus")
            {
                m_opensim = sim;
            }

            protected override byte[] ProcessRequest(string path, Stream request,
                                 IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
            {
                return Util.UTF8.GetBytes(m_opensim.StatReport(httpRequest));
            }

            public override string ContentType
            {
                get { return "text/plain"; }
            }
        }

        /// <summary>
        /// Handler to supply the current extended status of this sim to a user configured URI
        /// Sends the statistical data in a json serialization
        /// If the request contains a key, "callback" the response will be wrappend in the
        /// associated value for jsonp used with ajax/javascript
        /// </summary>
        protected class UXSimStatusHandler : BaseStreamHandler
        {
            OpenSimBase m_opensim;

            public UXSimStatusHandler(OpenSimBase sim)
                : base("GET", "/" + sim.userStatsURI, "UXSimStatus", "Simulator UXStatus")
            {
                m_opensim = sim;
            }

            protected override byte[] ProcessRequest(string path, Stream request,
                                 IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
            {
                return Util.UTF8.GetBytes(m_opensim.StatReport(httpRequest));
            }

            public override string ContentType
            {
                get { return "text/plain"; }
            }
        }

        /// <summary>
        /// handler to supply serving http://domainname:port/robots.txt
        /// </summary>
        public class SimRobotsHandler : BaseStreamHandler
        {
            public SimRobotsHandler() : base("GET", "/robots.txt", "SimRobots.txt", "Simulator Robots.txt") {}

            protected override byte[] ProcessRequest(string path, Stream request,
                                 IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
            {
                string robots = "# go away\nUser-agent: *\nDisallow: /\n";
                return Util.UTF8.GetBytes(robots);
            }

            public override string ContentType
            {
                get { return "text/plain"; }
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
            m_log.Info("[SHUTDOWN]: Closing console and terminating");

            try
            {
                SceneManager.Close();

                foreach (IApplicationPlugin plugin in m_plugins)
                    plugin.Dispose();
            }
            catch (Exception e)
            {
                m_log.Error("[SHUTDOWN]: Ignoring failure during shutdown - ", e);
            }

            base.ShutdownSpecific();
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
            if (!string.IsNullOrEmpty(estateName))
                newName = estateName;
            else
                newName = MainConsole.Instance.Prompt("New estate name", regInfo.EstateSettings.EstateName);

            if (estatesByName.ContainsKey(newName))
            {
                MainConsole.Instance.Output("An estate named {0} already exists.  Please try again.", null, newName);
                return false;
            }

            regInfo.EstateSettings.EstateName = newName;

            // FIXME: Later on, the scene constructor will reload the estate settings no matter what.
            // Therefore, we need to do an initial save here otherwise the new estate name will be reset
            // back to the default.  The reloading of estate settings by scene could be eliminated if it
            // knows that the passed in settings in RegionInfo are already valid.  Also, it might be
            // possible to eliminate some additional later saves made by callers of this method.
            EstateDataService.StoreEstateSettings(regInfo.EstateSettings);

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

            if (Config.Configs[ESTATE_SECTION_NAME] != null)
            {
                defaultEstateName = Config.Configs[ESTATE_SECTION_NAME].GetString("DefaultEstateName", null);

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
                        = MainConsole.Instance.Prompt(
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
                            = MainConsole.Instance.Prompt(
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
    }
}
