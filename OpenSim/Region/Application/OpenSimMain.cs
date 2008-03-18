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
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Timers;
using Nini.Config;
using OpenSim.Framework;
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
using Timer=System.Timers.Timer;
using System.Net;
using Nwc.XmlRpc;
using System.Collections;
using System.Reflection;
using libsecondlife;
using Mono.Addins;
using Mono.Addins.Description;

namespace OpenSim
{
    public delegate void ConsoleCommand(string[] comParams);

    public class OpenSimMain : RegionApplicationBase, conscmd_callback
    {
        private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private string proxyUrl;
        private int proxyOffset = 0;

        private const string DEFAULT_PRIM_BACKUP_FILENAME = "prim-backup.xml";

        public string m_physicsEngine;
        public string m_meshEngineName;
        public string m_scriptEngine;
        public bool m_sandbox;
        public bool user_accounts;
        public bool m_gridLocalAsset;
        public bool m_see_into_region_from_neighbor;

        protected LocalLoginService m_loginService;

        protected string m_storageDll;

        protected string m_startupCommandsFile;
        protected string m_shutdownCommandsFile;

        protected List<UDPServer> m_udpServers = new List<UDPServer>();
        protected List<RegionInfo> m_regionData = new List<RegionInfo>();

        private bool m_physicalPrim;
        private bool m_permissions = false;

        private bool m_standaloneAuthenticate = false;
        private string m_standaloneWelcomeMessage = null;
        private string m_standaloneInventoryPlugin;
        private string m_standaloneAssetPlugin;
        private string m_standaloneUserPlugin;

        private string m_assetStorage = "local";

        private string m_timedScript = "disabled";
        private Timer m_scriptTimer;

        public ConsoleCommand CreateAccount = null;
        private bool m_dumpAssetsToFile;

        private List<IApplicationPlugin> m_plugins = new List<IApplicationPlugin>();

        private IniConfigSource m_config;

        public IniConfigSource ConfigSource
        {
            get { return m_config; }
            set { m_config = value; }
        }

        public BaseHttpServer HttpServer
        {
            get { return m_httpServer; }
        }

        public List<UDPServer> UdpServers
        {
            get { return m_udpServers; }
        }

        public List<RegionInfo> RegionData
        {
            get { return m_regionData; }
        }

        private ModuleLoader m_moduleLoader;

        public ModuleLoader ModuleLoader
        {
            get { return m_moduleLoader; }
            set { m_moduleLoader = value; }
        }

        public OpenSimMain(IConfigSource configSource)
            : base()
        {
            IConfig startupConfig = configSource.Configs["Startup"];

            // The Mono addin manager (in Mono.Addins.dll version 0.2.0.0) occasionally seems to corrupt its addin cache
            // Hence, as a temporary solution we'll remove it before each startup
            if (Directory.Exists("addin-db-000"))
            {
                Directory.Delete("addin-db-000", true);
            }
            
            AddinManager.Initialize(".");
            AddinManager.Registry.Update(null);

            Application.iniFilePath = startupConfig.GetString("inifile", "OpenSim.ini");

            m_config = new IniConfigSource();
            //check for .INI file (either default or name passed in command line)
            if (File.Exists(Application.iniFilePath))
            {
                m_config.Merge(new IniConfigSource(Application.iniFilePath));
                m_config.Merge(configSource);
            }
            else
            {
                Application.iniFilePath = Path.Combine(Util.configDir(), Application.iniFilePath);
                if (File.Exists(Application.iniFilePath))
                {
                    m_config.Merge(new IniConfigSource(Application.iniFilePath));
                    m_config.Merge(configSource);
                }
                else
                {
                    // no default config files, so set default values, and save it
                    m_config.Merge(DefaultConfig());
                    m_config.Merge(configSource);
                    m_config.Save(Application.iniFilePath);
                }
            }

            ReadConfigSettings();
        }

        public static IConfigSource DefaultConfig()
        {
            IConfigSource DefaultConfig = new IniConfigSource();
            if (DefaultConfig.Configs["Startup"] == null)
                DefaultConfig.AddConfig("Startup");
            IConfig config = DefaultConfig.Configs["Startup"];
            if (config != null)
            {
                config.Set("gridmode", false);
                config.Set("physics", "basicphysics");
                config.Set("physical_prim", true);
                config.Set("see_into_this_sim_from_neighbor", true);
                config.Set("serverside_object_permissions", false);
                config.Set("storage_plugin", "OpenSim.Framework.Data.SQLite.dll");
                config.Set("storage_connection_string", "URI=file:OpenSim.db,version=3");
                config.Set("storage_prim_inventories", true);
                config.Set("startup_console_commands_file", String.Empty);
                config.Set("shutdown_console_commands_file", String.Empty);
                config.Set("script_engine", "OpenSim.Region.ScriptEngine.DotNetEngine.dll");
                config.Set("asset_database", "sqlite");
            }

            if (DefaultConfig.Configs["StandAlone"] == null)
                DefaultConfig.AddConfig("StandAlone");

            config = DefaultConfig.Configs["StandAlone"];
            if (config != null)
            {
                config.Set("accounts_authenticate", false);
                config.Set("welcome_message", "Welcome to OpenSim");
                config.Set("inventory_plugin", "OpenSim.Framework.Data.SQLite.dll");
                config.Set("userDatabase_plugin", "OpenSim.Framework.Data.SQLite.dll");
                config.Set("asset_plugin", "OpenSim.Framework.Data.SQLite.dll");
                config.Set("dump_assets_to_file", false);
            }

            if (DefaultConfig.Configs["Network"] == null)
                DefaultConfig.AddConfig("Network");
            config = DefaultConfig.Configs["Network"];
            if (config != null)
            {
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
            }

            if (DefaultConfig.Configs["RemoteAdmin"] == null)
                DefaultConfig.AddConfig("RemoteAdmin");
            config = DefaultConfig.Configs["RemoteAdmin"];
            if (config != null)
            {
                config.Set("enabled", "false");
            }
            return DefaultConfig;
        }

        protected void ReadConfigSettings()
        {
            m_networkServersInfo = new NetworkServersInfo();

            IConfig startupConfig = m_config.Configs["Startup"];

            if (startupConfig != null)
            {
                m_sandbox = !startupConfig.GetBoolean("gridmode", false);
                m_physicsEngine = startupConfig.GetString("physics", "basicphysics");
                m_meshEngineName = startupConfig.GetString("meshing", "ZeroMesher");
                
                m_physicalPrim = startupConfig.GetBoolean("physical_prim", true);

                m_see_into_region_from_neighbor = startupConfig.GetBoolean("see_into_this_sim_from_neighbor", true);

                m_permissions = startupConfig.GetBoolean("serverside_object_permissions", false);

                m_storageDll = startupConfig.GetString("storage_plugin", "OpenSim.Framework.Data.SQLite.dll");
                if (m_storageDll == "OpenSim.DataStore.MonoSqlite.dll") 
                {
                    m_storageDll = "OpenSim.Framework.Data.SQLite.dll";
                    Console.WriteLine("WARNING: OpenSim.DataStore.MonoSqlite.dll is deprecated. Set storage_plugin to OpenSim.Framework.Data.SQLite.dll.");
                    Thread.Sleep(3000);
                }
                m_storageConnectionString
                    = startupConfig.GetString("storage_connection_string", "URI=file:OpenSim.db,version=3");
                m_storagePersistPrimInventories
                    = startupConfig.GetBoolean("storage_prim_inventories", true);

                m_startupCommandsFile = startupConfig.GetString("startup_console_commands_file", String.Empty);
                m_shutdownCommandsFile = startupConfig.GetString("shutdown_console_commands_file", String.Empty);

                m_scriptEngine = startupConfig.GetString("script_engine", "OpenSim.Region.ScriptEngine.DotNetEngine.dll");
                m_assetStorage = startupConfig.GetString("asset_database", "local");

                m_timedScript = startupConfig.GetString("timer_Script", "disabled");
            }

            IConfig standaloneConfig = m_config.Configs["StandAlone"];
            if (standaloneConfig != null)
            {
                m_standaloneAuthenticate = standaloneConfig.GetBoolean("accounts_authenticate", false);
                m_standaloneWelcomeMessage = standaloneConfig.GetString("welcome_message", "Welcome to OpenSim");
                m_standaloneInventoryPlugin =
                    standaloneConfig.GetString("inventory_plugin", "OpenSim.Framework.Data.SQLite.dll");
                m_standaloneUserPlugin =
                    standaloneConfig.GetString("userDatabase_plugin", "OpenSim.Framework.Data.SQLite.dll");
                m_standaloneAssetPlugin =
                    standaloneConfig.GetString("asset_plugin", "OpenSim.Framework.Data.SQLite.dll");

                m_dumpAssetsToFile = standaloneConfig.GetBoolean("dump_assets_to_file", false);
            }
            //if (!m_sandbox)
                //m_SendChildAgentTaskData = false;

            m_networkServersInfo.loadFromConfiguration(m_config);
        }

        /// <summary>
        /// Performs initialisation of the scene, such as loading configuration from disk.
        /// </summary>
        public override void StartUp()
        {
            //
            // Called from app startup (OpenSim.Application)
            //
            
            m_log.Info("====================================================================");
            m_log.Info("========================= STARTING OPENSIM =========================");
            m_log.Info("====================================================================");
            m_log.InfoFormat("[OPENSIM MAIN]: Running in {0} mode", (m_sandbox ? "sandbox" : "grid"));
            
            m_console = CreateConsole();
            MainConsole.Instance = m_console;

            StatsManager.StartCollectingSimExtraStats();
            
            // Do baseclass startup sequence: OpenSim.Region.ClientStack.RegionApplicationBase.StartUp
            // TerrainManager, StorageManager, HTTP Server
            // This base will call abstract Initialize
            base.StartUp();

            // StandAlone mode? m_sandbox is determined by !startupConfig.GetBoolean("gridmode", false)
            if (m_sandbox)
            {
                LocalInventoryService inventoryService = new LocalInventoryService();
                inventoryService.AddPlugin(m_standaloneInventoryPlugin);

                LocalUserServices userService =
                    new LocalUserServices(m_networkServersInfo, m_networkServersInfo.DefaultHomeLocX,
                                          m_networkServersInfo.DefaultHomeLocY, inventoryService);
                userService.AddPlugin(m_standaloneUserPlugin);

                LocalBackEndServices backendService = new LocalBackEndServices();

                CommunicationsLocal localComms =
                    new CommunicationsLocal(m_networkServersInfo, m_httpServer, m_assetCache, userService,
                                            inventoryService, backendService, backendService, m_dumpAssetsToFile);
                m_commsManager = localComms;

                m_loginService =
                    new LocalLoginService(userService, m_standaloneWelcomeMessage, localComms, m_networkServersInfo,
                                          m_standaloneAuthenticate);
                m_loginService.OnLoginToRegion += backendService.AddNewSession;

                // XMLRPC action
                m_httpServer.AddXmlRPCHandler("login_to_simulator", m_loginService.XmlRpcLoginMethod);
                
                // provides the web form login
                m_httpServer.AddHTTPHandler("login", m_loginService.ProcessHTMLLogin);

                // Provides the LLSD login
                m_httpServer.SetLLSDHandler(m_loginService.LLSDLoginMethod);

                CreateAccount = localComms.doCreate;
            }
            else
            {
                // We are in grid mode
                m_commsManager = new CommunicationsOGS1(m_networkServersInfo, m_httpServer, m_assetCache);
                m_httpServer.AddStreamHandler(new SimStatusHandler());
            }

            proxyUrl = ConfigSource.Configs["Network"].GetString("proxy_url", "");
            proxyOffset = Int32.Parse(ConfigSource.Configs["Network"].GetString("proxy_offset", "0"));

            // Create a ModuleLoader instance
            m_moduleLoader = new ModuleLoader(m_config);

            ExtensionNodeList nodes = AddinManager.GetExtensionNodes("/OpenSim/Startup");
            foreach (TypeExtensionNode node in nodes)
            {
                m_log.InfoFormat("[PLUGINS]: Loading OpenSim application plugin {0}", node.Path);
                IApplicationPlugin plugin = (IApplicationPlugin)node.CreateInstance();
                plugin.Initialise(this);
                m_plugins.Add(plugin);
            }

            // Start UDP servers
            //for (int i = 0; i < m_udpServers.Count; i++)
            //{
            // m_udpServers[i].ServerListener();
            // }

            //Run Startup Commands
            if (m_startupCommandsFile != String.Empty)
            {
                RunCommandScript(m_startupCommandsFile);
            }
            else
            {
                m_log.Info("[STARTUP]: No startup command script specified. Moving on...");
            }

            // Start timer script (run a script every xx seconds)
            if (m_timedScript != "disabled")
            {
                m_scriptTimer = new Timer();
                m_scriptTimer.Enabled = true;
                m_scriptTimer.Interval = (int)(1200 * 1000);
                m_scriptTimer.Elapsed += new ElapsedEventHandler(RunAutoTimerScript);
            }

            // We are done with startup
            PrintFileToConsole("startuplogo.txt");
            m_log.Info("[OPENSIM MAIN]: Startup complete, serving " + m_udpServers.Count.ToString() + " region(s)");
        }

        protected override void Initialize()
        {
            //
            // Called from base.StartUp()
            //

            m_httpServerPort = m_networkServersInfo.HttpListenerPort;

            IAssetServer assetServer;
            if (m_assetStorage == "db4o")
            {
                assetServer = new LocalAssetServer();
            }
            else if (m_assetStorage == "grid")
            {
                assetServer = new GridAssetClient(m_networkServersInfo.AssetURL);
            }
            else
            {
                SQLAssetServer sqlAssetServer = new SQLAssetServer(m_standaloneAssetPlugin);
                sqlAssetServer.LoadDefaultAssets();
                assetServer = sqlAssetServer;
            }

            m_assetCache = new AssetCache(assetServer);

            m_sceneManager.OnRestartSim += handleRestartRegion;
        }

        public LLUUID CreateUser(string tempfirstname, string templastname, string tempPasswd, uint regX, uint regY)
        {
            return m_commsManager.AddUser(tempfirstname,templastname,tempPasswd,regX,regY);
        }

        public UDPServer CreateRegion(RegionInfo regionInfo, bool portadd_flag)
        {
            int port = regionInfo.InternalEndPoint.Port;

            // set initial RegionID to originRegionID in RegionInfo. (it needs for loding prims)
            regionInfo.originRegionID = regionInfo.RegionID;

            // set initial ServerURI
            regionInfo.ServerURI = "http://" + regionInfo.ExternalHostName 
                                        + ":" + regionInfo.InternalEndPoint.Port.ToString();

            if ((proxyUrl.Length > 0) && (portadd_flag)) 
            {
                // set proxy url to RegionInfo
                regionInfo.proxyUrl = proxyUrl;
                ProxyCommand(proxyUrl, "AddPort", port, port + proxyOffset, regionInfo.ExternalHostName);
            }

            UDPServer udpServer;
            Scene scene = SetupScene(regionInfo, proxyOffset, out udpServer, m_permissions);

            m_log.Info("[MODULES]: Loading Region's modules");

            m_moduleLoader.PickupModules(scene, ".");
            //m_moduleLoader.PickupModules(scene, "ScriptEngines");
            //m_moduleLoader.LoadRegionModules(Path.Combine("ScriptEngines", m_scriptEngine), scene);

            if (string.IsNullOrEmpty(m_scriptEngine))
            {
                m_log.Info("[MODULES]: No script engien module specified");
            }
            else
            {
                m_log.Info("[MODULES]: Loading scripting engine modules");
                foreach (string module in m_scriptEngine.Split(','))
                {
                    string mod = module.Trim(" \t".ToCharArray()); // Clean up name
                    m_log.Info("[MODULES]: Loading scripting engine: " + mod);
                    try
                    {
                        m_moduleLoader.LoadRegionModules(Path.Combine("ScriptEngines", mod), scene);
                    }
                    catch (Exception ex)
                    {
                        m_log.Error("[MODULES]: Failed to load script engine: " + ex.ToString());
                    }
                }
            }

            m_moduleLoader.InitialiseSharedModules(scene);
            scene.SetModuleInterfaces();

            //Server side object editing permissions checking
            scene.PermissionsMngr.BypassPermissions = !m_permissions;
            
            // We need to do this after we've initialized the scripting engines.
            scene.StartScripts();            

            m_sceneManager.Add(scene);                        

            m_udpServers.Add(udpServer);
            m_regionData.Add(regionInfo);
            udpServer.ServerListener();

            return udpServer;
        }

        protected override StorageManager CreateStorageManager(string connectionstring)
        {
            return new StorageManager(m_storageDll, connectionstring, m_storagePersistPrimInventories);
        }

        protected override Scene CreateScene(RegionInfo regionInfo, StorageManager storageManager,
                                             AgentCircuitManager circuitManager)
        {
            PermissionManager permissionManager = new PermissionManager();
            SceneCommunicationService sceneGridService = new SceneCommunicationService(m_commsManager);
            return
                new Scene(regionInfo, circuitManager, permissionManager, m_commsManager, sceneGridService, m_assetCache,
                          storageManager, m_httpServer,
                          m_moduleLoader, m_dumpAssetsToFile, m_physicalPrim, m_see_into_region_from_neighbor);
        }


        public void handleRestartRegion(RegionInfo whichRegion)
        {
            m_log.Error("[OPENSIM MAIN]: Got restart signal from SceneManager");
            // Shutting down the UDP server
            bool foundUDPServer = false;
            int UDPServerElement = 0;

            for (int i = 0; i < m_udpServers.Count; i++)
            {
                if (m_udpServers[i].RegionHandle == whichRegion.RegionHandle)
                {
                    UDPServerElement = i;
                    foundUDPServer = true;
                    break;
                }
            }
            if (foundUDPServer)
            {
                // m_udpServers[UDPServerElement].Server.End
                m_udpServers[UDPServerElement].Server.Close();
                m_udpServers.RemoveAt(UDPServerElement);
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
            //UDPServer restartingRegion = CreateRegion(whichRegion);
            //restartingRegion.ServerListener();
            //m_sceneManager.SendSimOnlineNotification(restartingRegion.RegionHandle);
        }

        protected override ConsoleBase CreateConsole()
        {
            return new ConsoleBase("Region", this);
        }

        # region Setup methods

        protected override PhysicsScene GetPhysicsScene()
        {
            return GetPhysicsScene(m_physicsEngine, m_meshEngineName);
        }

        private class SimStatusHandler : IStreamedRequestHandler
        {
            public byte[] Handle(string path, Stream request)
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
        public virtual void Shutdown()
        {
            ProxyCommand(proxyUrl, "Stop"); 
			
            if (m_startupCommandsFile != String.Empty)
            {
                RunCommandScript(m_shutdownCommandsFile);
            }

            m_log.Info("[SHUTDOWN]: Closing all threads");
            m_log.Info("[SHUTDOWN]: Killing listener thread");
            m_log.Info("[SHUTDOWN]: Killing clients");
            // TODO: implement this
            m_log.Info("[SHUTDOWN]: Closing console and terminating");

            m_sceneManager.Close();

            m_console.Close();
            Environment.Exit(0);
		}

        private void RunAutoTimerScript(object sender, EventArgs e)
        {
            if (m_timedScript != "disabled")
            {
                RunCommandScript(m_timedScript);
            }
        }

        #region Console Commands

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName"></param>
        private void RunCommandScript(string fileName)
        {
            m_log.Info("[COMMANDFILE]: Running " + fileName);
            if (File.Exists(fileName))
            {
                StreamReader readFile = File.OpenText(fileName);
                string currentCommand = String.Empty;
                while ((currentCommand = readFile.ReadLine()) != null)
                {
                    if (currentCommand != String.Empty)
                    {
                        m_log.Info("[COMMANDFILE]: Running '" + currentCommand + "'");
                        m_console.RunCommand(currentCommand);
                    }
                }
            }
            else
            {
                m_log.Error("[COMMANDFILE]: Command script missing. Can not run commands");
            }
        }

        private void PrintFileToConsole(string fileName)
        {
            if (File.Exists(fileName))
            {
                StreamReader readFile = File.OpenText(fileName);
                string currentLine = String.Empty;
                while ((currentLine = readFile.ReadLine()) != null)
                {
                    m_log.Info("[!]" + currentLine);
                }
            }
        }


        /// <summary>
        /// Runs commands issued by the server console from the operator
        /// </summary>
        /// <param name="command">The first argument of the parameter (the command)</param>
        /// <param name="cmdparams">Additional arguments passed to the command</param>
        public override void RunCmd(string command, string[] cmdparams)
        {
            base.RunCmd(command, cmdparams);
            
            switch (command)
            {
                case "clear-assets":
                    m_assetCache.Clear();
                    break;

                case "set-time":
                    m_sceneManager.SetCurrentSceneTimePhase(Convert.ToInt32(cmdparams[0]));
                    break;

                case "force-update":
                    m_console.Notice("Updating all clients");
                    m_sceneManager.ForceCurrentSceneClientUpdate();
                    break;

                case "edit-scale":
                    if (cmdparams.Length == 4)
                    {
                        m_sceneManager.HandleEditCommandOnCurrentScene(cmdparams);
                    }
                    break;

                case "debug":
                    if (cmdparams.Length > 0)
                    {
                        Debug(cmdparams);
                    }
                    break;

		case "scene-debug":
			if (cmdparams.Length == 3) {
				if (m_sceneManager.CurrentScene == null) {
					m_console.Error("CONSOLE", "Please use 'change-region <regioname>' first");
				} else {
					m_sceneManager.CurrentScene.SetSceneCoreDebug(!System.Convert.ToBoolean(cmdparams[0]), !System.Convert.ToBoolean(cmdparams[1]), !System.Convert.ToBoolean(cmdparams[2]));
				}
			} else {
				m_console.Error("scene-debug <scripting> <collisions> <physics> (where inside <> is true/false)");
			}
			break;

                case "help":
                    m_console.Notice("alert - send alert to a designated user or all users.");
                    m_console.Notice("  alert [First] [Last] [Message] - send an alert to a user. Case sensitive.");
                    m_console.Notice("  alert general [Message] - send an alert to all users.");
                    m_console.Notice("backup - trigger a simulator backup");
                    m_console.Notice("clear-assets - clear asset cache");
                    m_console.Notice("create user - adds a new user.");
                    m_console.Notice("change-region [name] - sets the region that many of these commands affect.");
                    m_console.Notice("command-script [filename] - Execute command in a file.");
                    m_console.Notice("debug - debugging commands");
                    m_console.Notice("  packet 0..255 - print incoming/outgoing packets (0=off)");
                    m_console.Notice("scene-debug [scripting] [collision] [physics] - Enable/Disable debug stuff, each can be True/False");
                    m_console.Notice("edit-scale [prim name] [x] [y] [z] - resize given prim");
                    m_console.Notice("export-map [filename] - save image of world map");
                    m_console.Notice("force-update - force an update of prims in the scene");
                    m_console.Notice("load-xml [filename] - load prims from XML");
                    m_console.Notice("load-xml2 [filename] - load prims from XML using version 2 format");
                    m_console.Notice("permissions [true/false] - turn on/off permissions on the scene");
                    m_console.Notice("quit - equivalent to shutdown.");
                    m_console.Notice("restart - disconnects all clients and restarts the sims in the instance.");
                    m_console.Notice("remove-region [name] - remove a region");
                    m_console.Notice("save-xml [filename] - save prims to XML");
                    m_console.Notice("save-xml2 [filename] - save prims to XML using version 2 format");
                    m_console.Notice("script - manually trigger scripts? or script commands?");
                    m_console.Notice("set-time [x] - set the current scene time phase");
                    m_console.Notice("show assets - show state of asset cache.");
                    m_console.Notice("show users - show info about connected users.");
                    m_console.Notice("show modules - shows info about loaded modules.");
                    m_console.Notice("show stats - statistical information for this server not displayed in the client");
                    m_console.Notice("threads - list threads");
                    m_console.Notice("shutdown - disconnect all clients and shutdown.");
                    m_console.Notice("config set section field value - set a config value");
                    m_console.Notice("config get section field - get a config value");
                    m_console.Notice("config save - save OpenSim.ini");
                    m_console.Notice("terrain help - show help for terrain commands.");
                    break;

                case "threads":
                        //m_console.Notice("THREAD", Process.GetCurrentProcess().Threads.Count + " threads running:");
                        //int _tc = 0;
                    
                        //foreach (ProcessThread pt in Process.GetCurrentProcess().Threads)
                        //{
                        //    _tc++;
                        //    m_console.Notice("THREAD", _tc + ": ID: " + pt.Id + ", Started: " + pt.StartTime.ToString() + ", CPU time: " + pt.TotalProcessorTime + ", Pri: " + pt.BasePriority.ToString() + ", State: " + pt.ThreadState.ToString());
                            
                        //}
                    List<Thread> threads = OpenSim.Framework.ThreadTracker.GetThreads();
                    if (threads == null)
                    {
                        m_console.Notice("THREAD", "Thread tracking is only enabled in DEBUG mode.");
                    }
                    else
                    {
                        int _tc = 0;
                        m_console.Notice("THREAD", threads.Count + " threads are being tracked:");
                        foreach (Thread t in threads)
                        {
                            _tc++;
                            m_console.Notice("THREAD", _tc + ": ID: " + t.ManagedThreadId.ToString() + ", Name: " + t.Name + ", Alive: " + t.IsAlive.ToString() + ", Pri: " + t.Priority.ToString() + ", State: " + t.ThreadState.ToString());
                        }
                    }


                    break;
                case "save-xml":
                    if (cmdparams.Length > 0)
                    {
                        m_sceneManager.SaveCurrentSceneToXml(cmdparams[0]);
                    }
                    else
                    {
                        m_sceneManager.SaveCurrentSceneToXml(DEFAULT_PRIM_BACKUP_FILENAME);
                    }
                    break;

                case "load-xml":
                    LLVector3 loadOffset = new LLVector3(0, 0, 0);
                    if (cmdparams.Length > 0)
                    {
                        bool generateNewIDS = false;
                        if (cmdparams.Length > 1)
                        {
                            if (cmdparams[1] == "-newUID")
                            {
                                generateNewIDS = true;
                            }
                            if (cmdparams.Length > 2)
                            {
                                loadOffset.X = (float) Convert.ToDecimal(cmdparams[2]);
                                if (cmdparams.Length > 3)
                                {
                                    loadOffset.Y = (float) Convert.ToDecimal(cmdparams[3]);
                                }
                                if (cmdparams.Length > 4)
                                {
                                    loadOffset.Z = (float) Convert.ToDecimal(cmdparams[4]);
                                }
                                m_console.Error("loadOffsets <X,Y,Z> = <" + loadOffset.X + "," + loadOffset.Y + "," +
                                                    loadOffset.Z + ">");
                            }
                        }
                        m_sceneManager.LoadCurrentSceneFromXml(cmdparams[0], generateNewIDS, loadOffset);
                    }
                    else
                    {
                        m_sceneManager.LoadCurrentSceneFromXml(DEFAULT_PRIM_BACKUP_FILENAME, false, loadOffset);
                    }
                    break;

                case "save-xml2":
                    if (cmdparams.Length > 0)
                    {
                        m_sceneManager.SaveCurrentSceneToXml2(cmdparams[0]);
                    }
                    else
                    {
                        m_sceneManager.SaveCurrentSceneToXml2(DEFAULT_PRIM_BACKUP_FILENAME);
                    }
                    break;

                case "load-xml2":
                    if (cmdparams.Length > 0)
                    {
                        m_sceneManager.LoadCurrentSceneFromXml2(cmdparams[0]);
                    }
                    else
                    {
                        m_sceneManager.LoadCurrentSceneFromXml2(DEFAULT_PRIM_BACKUP_FILENAME);
                    }
                    break;

                case "terrain":
                    string result = String.Empty;
            
                    if (!m_sceneManager.RunTerrainCmdOnCurrentScene(cmdparams, ref result))
                    {
                        m_console.Error(result);
                    }
                    break;

                case "script":
                    m_sceneManager.SendCommandToCurrentSceneScripts(cmdparams);
                    break;

                case "command-script":
                    if (cmdparams.Length > 0)
                    {
                        RunCommandScript(cmdparams[0]);
                    }
                    break;

                case "permissions":
                    // Treats each user as a super-admin when disabled
                    bool permissions = Convert.ToBoolean(cmdparams[0]);
                    m_sceneManager.SetBypassPermissionsOnCurrentScene(!permissions);
                    break;

                case "backup":
                    m_sceneManager.BackupCurrentScene();
                    break;

                case "alert":
                    m_sceneManager.HandleAlertCommandOnCurrentScene(cmdparams);
                    break;

                case "create":
                    CreateAccount(cmdparams);
                    break;

                case "create-region":
                    CreateRegion(new RegionInfo(cmdparams[0], "Regions/" + cmdparams[1],false), true);
                    break;
                case "remove-region":
                    string regName = CombineParams(cmdparams, 0);

                    Scene killScene;
                    if (m_sceneManager.TryGetScene(regName, out killScene))
                    {
                        if (m_sceneManager.CurrentScene.RegionInfo.RegionID == killScene.RegionInfo.RegionID)
                        {
                            m_sceneManager.TrySetCurrentScene("..");
                        }
                        m_regionData.Remove(killScene.RegionInfo);
                        m_sceneManager.CloseScene(killScene);
                    }
                    break;

                case "quit":
                case "shutdown":
                    Shutdown();
                    break;

                case "restart":
                    m_sceneManager.RestartCurrentScene();
                    break;

                case "change-region":
                    if (cmdparams.Length > 0)
                    {
                        string regionName = CombineParams(cmdparams, 0);

                        if (!m_sceneManager.TrySetCurrentScene(regionName))
                        {
                            m_console.Error("Couldn't set current region to: " + regionName);
                        }
                    }

                    if (m_sceneManager.CurrentScene == null)
                    {
                        m_console.Error("CONSOLE", "Currently at Root level. To change region please use 'change-region <regioname>'");
                    }
                    else
                    {
                        m_console.Error("CONSOLE", "Current Region: " + m_sceneManager.CurrentScene.RegionInfo.RegionName +
                                        ". To change region please use 'change-region <regioname>'");
                    }

                    break;

                case "export-map":
                    if (cmdparams.Length > 0)
                    {
                        m_sceneManager.CurrentOrFirstScene.ExportWorldMap(cmdparams[0]);
                    }
                    else
                    {
                        m_sceneManager.CurrentOrFirstScene.ExportWorldMap("exportmap.jpg");
                    }
                    break;

                case "config":
                    string n = command.ToUpper();
                    if (cmdparams.Length > 0)
                    {
                        switch (cmdparams[0].ToLower())
                        {
                            case "set":
                                if (cmdparams.Length < 4)
                                {
                                    m_console.Error(n, "SYNTAX: " + n + " SET SECTION KEY VALUE");
                                    m_console.Error(n, "EXAMPLE: " + n + " SET ScriptEngine.DotNetEngine NumberOfScriptThreads 5");
                                }
                                else
                                {
                                    IConfig c = DefaultConfig().Configs[cmdparams[1]];
                                    if (c == null)
                                        c = DefaultConfig().AddConfig(cmdparams[1]);
                                    string _value = String.Join(" ", cmdparams, 3, cmdparams.Length - 3);
                                    c.Set(cmdparams[2], _value);
                                    m_config.Merge(c.ConfigSource);

                                    m_console.Error(n, n + " " + n + " " + cmdparams[1] + " " + cmdparams[2] + " " +
                                                    _value);
                                }
                                break;
                            case "get":
                                if (cmdparams.Length < 3)
                                {
                                    m_console.Error(n, "SYNTAX: " + n + " GET SECTION KEY");
                                    m_console.Error(n, "EXAMPLE: " + n + " GET ScriptEngine.DotNetEngine NumberOfScriptThreads");
                                }
                                else
                                {
                                    IConfig c = DefaultConfig().Configs[cmdparams[1]];
                                    if (c == null)
                                    {
                                        m_console.Notice(n, "Section \"" + cmdparams[1] + "\" does not exist.");
                                        break;
                                    }
                                    else
                                    {
                                        m_console.Notice(n + " GET " + cmdparams[1] + " " + cmdparams[2] + ": " +
                                                         c.GetString(cmdparams[2]));
                                    }
                                }

                                break;
                            case "save":
                                m_console.Notice("Saving configuration file: " + Application.iniFilePath);
                                m_config.Save(Application.iniFilePath);
                                break;
                        }
                    }
                    break;
                case "modules":
                    if (cmdparams.Length > 0)
                    {
                        switch (cmdparams[0].ToLower())
                        {
                            case "list":
                                foreach (IRegionModule irm in m_moduleLoader.GetLoadedSharedModules)
                                {
                                    m_console.Notice("Shared region module: " + irm.Name);
                                }
                                break;
                            case "unload":
                                if (cmdparams.Length > 1)
                                {
                                    foreach (IRegionModule rm in new System.Collections.ArrayList(m_moduleLoader.GetLoadedSharedModules))
                                    {
                                        if (rm.Name.ToLower() == cmdparams[1].ToLower())
                                        {
                                            m_console.Notice("Unloading module: " + rm.Name);
                                            m_moduleLoader.UnloadModule(rm);
                                        }
                                    }
                                }
                                break;
                            case "load":
                                if (cmdparams.Length > 1)
                                {
                                    foreach (Scene s in new System.Collections.ArrayList(m_sceneManager.Scenes))
                                    {
                                        
                                        m_console.Notice("Loading module: " + cmdparams[1]);
                                        m_moduleLoader.LoadRegionModules(cmdparams[1], s);
                                    }
                                }
                                break;
                        }
                    }

                    break;                    
                    /*
                     * Temporarily disabled but it would be good to have this - needs to be levered
                     * in to BaseOpenSimServer (which requires a RunCmd method restrcuture probably)
                default:
                    m_console.Error("Unknown command");
                    break;
                    */
            }
        }

        public void Debug(string[] args)
        {
            switch (args[0])
            {
                case "packet":
                    if (args.Length > 1)
                    {
                        int newDebug;
                        if (int.TryParse(args[1], out newDebug))
                        {
                            m_sceneManager.SetDebugPacketOnCurrentScene(newDebug);
                        }
                        else
                        {
                            m_console.Error("packet debug should be 0..2");
                        }
                        m_console.Notice("New packet debug: " + newDebug.ToString());
                    }

                    break;
                default:
                    m_console.Error("Unknown debug");
                    break;
            }
        }

        // see BaseOpenSimServer
        public override void Show(string ShowWhat)
        {
            base.Show(ShowWhat);
            
            switch (ShowWhat)
            {
                case "assets":
                    m_assetCache.ShowState();
                    break;

                case "users":
                    m_console.Notice(
                        String.Format("{0,-16}{1,-16}{2,-37}{3,-16}{4,-22}{5,-16}", "Firstname", "Lastname",
                                      "Agent ID", "Circuit", "IP", "Region"));

                    foreach (ScenePresence presence in m_sceneManager.GetCurrentSceneAvatars())
                    {
                        RegionInfo regionInfo = m_sceneManager.GetRegionInfo(presence.RegionHandle);
                        string regionName;
                        System.Net.EndPoint ep = null;

                        if (regionInfo == null)
                        {
                            regionName = "Unresolvable";
                        }
                        else
                        {
                            regionName = regionInfo.RegionName;
                        }

                        for (int i = 0; i < m_udpServers.Count; i++)
                        {
                            if (m_udpServers[i].RegionHandle == presence.RegionHandle)
                            {

                                m_udpServers[i].clientCircuits_reverse.TryGetValue(presence.ControllingClient.CircuitCode, out ep);
                            }
                        }

                        m_console.Notice(
                            String.Format("{0,-16}{1,-16}{2,-37}{3,-16}{4,-22}{5,-16}",
                                          presence.Firstname,
                                          presence.Lastname,
                                          presence.UUID,
                                          presence.ControllingClient.CircuitCode,
                                          ep,
                                          regionName));
                        m_console.Notice("        {0}", (((ClientView)presence.ControllingClient).PacketProcessingEnabled)?"Active client":"Standby client");

                    }

                    break;
                case "modules":
                    m_console.Notice("The currently loaded shared modules are:");
                    foreach (IRegionModule module in m_moduleLoader.GetLoadedSharedModules)
                    {
                        m_console.Notice("Shared Module: " + module.Name);
                    }
                    break;

                case "regions":
                    m_sceneManager.ForEachScene(
                        delegate(Scene scene)
                            {
                                m_console.Notice("Region Name: " + scene.RegionInfo.RegionName + " , Region XLoc: " +
                                                 scene.RegionInfo.RegionLocX + " , Region YLoc: " +
                                                 scene.RegionInfo.RegionLocY);
                            });
                    break;
                                    
                case "stats":
                    if (StatsManager.SimExtraStats != null)
                    {
                        m_console.Notice(
                            "STATS", Environment.NewLine + StatsManager.SimExtraStats.Report());                    
                    }
                    else
                    {
                        m_console.Notice("Extra sim statistics collection has not been enabled");
                    }
                    break;                    
            }
        }

        private string CombineParams(string[] commandParams, int pos)
        {
            string result = String.Empty;
            for (int i = pos; i < commandParams.Length; i++)
            {
                result += commandParams[i] + " ";
            }
            result = result.TrimEnd(' ');
            return result;
        }

        #endregion
		// TODO: remove me!! (almost same as XmlRpcCommand)
        public object ProxyCommand(string url, string methodName, params object[] args)
        {
            if(proxyUrl.Length==0) return null;
            return SendXmlRpcCommand(url, methodName, args);
        }

        public object XmlRpcCommand(uint port, string methodName, params object[] args)
        {
            return SendXmlRpcCommand("http://localhost:"+port, methodName, args);
        }

        public object XmlRpcCommand(string url, string methodName, params object[] args)
        {
            return SendXmlRpcCommand(url, methodName, args);
        }

        private object SendXmlRpcCommand(string url, string methodName, object[] args)
        {
            try {
                //MainLog.Instance.Verbose("XMLRPC", "Sending command {0} to {1}", methodName, url);
                XmlRpcRequest client = new XmlRpcRequest(methodName, args);
                //MainLog.Instance.Verbose("XMLRPC", client.ToString());
                XmlRpcResponse response = client.Send(url, 6000);
                if(!response.IsFault) return response.Value;
            }
            catch(Exception e)
            {
                m_log.ErrorFormat("XMLRPC Failed to send command {0} to {1}: {2}", methodName, url, e.Message);
            }
            return null;
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
        /// Get the number of the avatars in the Region server
        /// </summary>
        /// <param name="usernum">The first out parameter describing the number of all the avatars in the Region server</param>
        public void GetRegionNumber(out int regionnum)
		{
			int accounter = 0;
			//List<string> regionNameList = new List<string>();

			m_sceneManager.ForEachScene(delegate(Scene scene) {
				accounter++;
			});
			regionnum = accounter;

		}
    }
}
