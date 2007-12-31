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
using System.IO;
using System.Text;
using System.Threading;
using System.Timers;
using libsecondlife;
using Mono.Addins;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Console;
using OpenSim.Framework.Servers;
using OpenSim.Region.ClientStack;
using OpenSim.Region.Communications.Local;
using OpenSim.Region.Communications.OGS1;
using OpenSim.Region.Environment;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Physics.Manager;
using Timer=System.Timers.Timer;

namespace OpenSim
{
    public delegate void ConsoleCommand(string[] comParams);

    public class OpenSimMain : RegionApplicationBase, conscmd_callback
    {
        private const string DEFAULT_PRIM_BACKUP_FILENAME = "prim-backup.xml";

        public string m_physicsEngine;
        public string m_meshEngineName;
        public string m_scriptEngine;
        public bool m_sandbox;
        public bool user_accounts;
        public bool m_gridLocalAsset;
        public bool m_SendChildAgentTaskData;

        protected LocalLoginService m_loginService;

        protected string m_storageDll;

        protected string m_startupCommandsFile;
        protected string m_shutdownCommandsFile;

        protected List<UDPServer> m_udpServers = new List<UDPServer>();
        protected List<RegionInfo> m_regionData = new List<RegionInfo>();

        private bool m_verbose;
        private bool m_physicalPrim;
        private readonly string m_logFilename = "region-console.log";
        private bool m_permissions = false;

        private bool m_standaloneAuthenticate = false;
        private string m_standaloneWelcomeMessage = null;
        private string m_standaloneInventoryPlugin;
        private string m_standaloneAssetPlugin;
        private string m_standaloneUserPlugin;

        private string m_assetStorage = "sqlite";

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

            AddinManager.Initialize(".");
            AddinManager.Registry.Update(null);

            string iniFilePath = startupConfig.GetString("inifile", "OpenSim.ini");

            m_config = new IniConfigSource();
            //check for .INI file (either default or name passed in command line)
            if (File.Exists(iniFilePath))
            {
                m_config.Merge(new IniConfigSource(iniFilePath));
                m_config.Merge(configSource);
            }
            else
            {
                iniFilePath = Path.Combine(Util.configDir(), iniFilePath);
                if (File.Exists(iniFilePath))
                {
                    m_config.Merge(new IniConfigSource(iniFilePath));
                    m_config.Merge(configSource);
                }
                else
                {
                    // no default config files, so set default values, and save it
                    m_config.Merge(DefaultConfig());

                    m_config.Merge(configSource);

                    m_config.Save(iniFilePath);
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
                config.Set("verbose", true);
                config.Set("physical_prim", true);
                config.Set("child_get_tasks", false);
                config.Set("serverside_object_permissions", false);
                config.Set("storage_plugin", "OpenSim.DataStore.MonoSqlite.dll");
                config.Set("storage_connection_string", "URI=file:OpenSim.db,version=3");
                config.Set("startup_console_commands_file", "");
                config.Set("shutdown_console_commands_file", "");
                config.Set("script_engine", "DotNetEngine");
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
                m_verbose = startupConfig.GetBoolean("verbose", true);

                m_physicalPrim = startupConfig.GetBoolean("physical_prim", true);

                m_SendChildAgentTaskData = startupConfig.GetBoolean("child_get_tasks", false);

                m_permissions = startupConfig.GetBoolean("serverside_object_permissions", false);

                m_storageDll = startupConfig.GetString("storage_plugin", "OpenSim.DataStore.MonoSqlite.dll");
                m_storageConnectionString
                    = startupConfig.GetString("storage_connection_string", "URI=file:OpenSim.db,version=3");

                m_startupCommandsFile = startupConfig.GetString("startup_console_commands_file", "");
                m_shutdownCommandsFile = startupConfig.GetString("shutdown_console_commands_file", "");

                m_scriptEngine = startupConfig.GetString("script_engine", "DotNetEngine");

                m_assetStorage = startupConfig.GetString("asset_database", "sqlite");

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
            if (!m_sandbox)
                m_SendChildAgentTaskData = false;


            m_networkServersInfo.loadFromConfiguration(m_config);
        }

        /// <summary>
        /// Performs initialisation of the scene, such as loading configuration from disk.
        /// </summary>
        public override void StartUp()
        {
            if (!Directory.Exists(Util.logDir()))
            {
                Directory.CreateDirectory(Util.logDir());
            }

            m_log = CreateLog();
            MainLog.Instance = m_log;

            base.StartUp();

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

                m_httpServer.AddXmlRPCHandler("login_to_simulator", m_loginService.XmlRpcLoginMethod);

                if (m_standaloneAuthenticate)
                {
                    CreateAccount = localComms.doCreate;
                }
            }
            else
            {
                m_commsManager = new CommunicationsOGS1(m_networkServersInfo, m_httpServer, m_assetCache);
                m_httpServer.AddStreamHandler(new SimStatusHandler());
            }

            m_moduleLoader = new ModuleLoader(m_log, m_config);

            ExtensionNodeList nodes = AddinManager.GetExtensionNodes("/OpenSim/Startup");
            MainLog.Instance.Verbose("PLUGINS", "Loading {0} OpenSim application plugins", nodes.Count);
            
            foreach (TypeExtensionNode node in nodes)
            {                    
                IApplicationPlugin plugin = (IApplicationPlugin) node.CreateInstance();
                                
                // Debug code to try and track down a bizzare ubuntu/mono/linux bug on standalone where we
                // appear to try and initialize all the plugins twice.  Currently disabled
//                MainLog.Instance.Verbose("PLUGINS", "Hitting plugin {0}", plugin.ToString());
//                if (m_plugins.Contains(plugin))
//                {
//                    MainLog.Instance.Verbose("PLUGINS", "Skipping {0}", plugin.ToString());
//                }
//                else
//                {
                    plugin.Initialise(this);
                    m_plugins.Add(plugin);
//                }
            }

            // Start UDP servers
            //for (int i = 0; i < m_udpServers.Count; i++)
            //{
            // m_udpServers[i].ServerListener();
            // }

            //Run Startup Commands
            if (m_startupCommandsFile != "")
            {
                RunCommandScript(m_startupCommandsFile);
            }
            else
            {
                MainLog.Instance.Verbose("STARTUP", "No startup command script specified. Moving on...");
            }

            MainLog.Instance.Status("STARTUP",
                                    "Startup complete, serving " + m_udpServers.Count.ToString() + " region(s)");

            if (m_timedScript != "disabled")
            {
                m_scriptTimer = new Timer();
                m_scriptTimer.Enabled = true;
                m_scriptTimer.Interval = (int) (1200*1000);
                m_scriptTimer.Elapsed += new ElapsedEventHandler(RunAutoTimerScript);
            }
        }

        public UDPServer CreateRegion(RegionInfo regionInfo)
        {
            UDPServer udpServer;
            Scene scene = SetupScene(regionInfo, out udpServer, m_permissions);

            MainLog.Instance.Verbose("MODULES", "Loading Region's Modules");

            m_moduleLoader.PickupModules(scene, ".");
            //m_moduleLoader.PickupModules(scene, "ScriptEngines");
            m_moduleLoader.LoadRegionModules(Path.Combine("ScriptEngine", "OpenSim.Region.ScriptEngine.DotNetEngine.dll"), scene);
            

            m_moduleLoader.InitialiseSharedModules(scene);
            scene.SetModuleInterfaces();

            //Server side object editing permissions checking
            scene.PermissionsMngr.BypassPermissions = !m_permissions;

            m_sceneManager.Add(scene);

            m_udpServers.Add(udpServer);
            m_regionData.Add(regionInfo);
            udpServer.ServerListener();

            return udpServer;
        }

        private static void CreateDefaultRegionInfoXml(string fileName)
        {
            new RegionInfo("DEFAULT REGION CONFIG", fileName);
        }

        protected override StorageManager CreateStorageManager(string connectionstring)
        {
            return new StorageManager(m_storageDll, connectionstring);
        }

        protected override Scene CreateScene(RegionInfo regionInfo, StorageManager storageManager,
                                             AgentCircuitManager circuitManager)
        {
            PermissionManager permissionManager = new PermissionManager();
            SceneCommunicationService sceneGridService = new SceneCommunicationService(m_commsManager);
            if (m_SendChildAgentTaskData)
            {
                MainLog.Instance.Error("WARNING",
                                       "Send Child Agent Task Updates is enabled. This is for testing only.  It doesn't work on grid mode!");
                Thread.Sleep(12000);
            }
            return
                new Scene(regionInfo, circuitManager, permissionManager, m_commsManager, sceneGridService, m_assetCache,
                          storageManager, m_httpServer,
                          m_moduleLoader, m_dumpAssetsToFile, m_physicalPrim, m_SendChildAgentTaskData);
        }

        protected override void Initialize()
        {
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
            else if (m_assetStorage == "mssql")
            {
                SQLAssetServer sqlAssetServer = new SQLAssetServer("OpenSim.Framework.Data.MSSQL.dll");
                sqlAssetServer.LoadDefaultAssets();
                assetServer = sqlAssetServer;
                //assetServer = new GridAssetClient("");
            }
            else
            {
                SQLAssetServer sqlAssetServer = new SQLAssetServer(m_standaloneAssetPlugin);
                sqlAssetServer.LoadDefaultAssets();
                assetServer = sqlAssetServer;
            }

            m_assetCache = new AssetCache(assetServer, m_log);
            // m_assetCache = new assetCache("OpenSim.Region.GridInterfaces.Local.dll", m_networkServersInfo.AssetURL, m_networkServersInfo.AssetSendKey);
            m_sceneManager.OnRestartSim += handleRestartRegion;
        }

        public void handleRestartRegion(RegionInfo whichRegion)
        {
            MainLog.Instance.Error("MAIN", "Got restart signal from SceneManager");
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

            CreateRegion(whichRegion);
            //UDPServer restartingRegion = CreateRegion(whichRegion);
            //restartingRegion.ServerListener();
            //m_sceneManager.SendSimOnlineNotification(restartingRegion.RegionHandle);
        }

        protected override LogBase CreateLog()
        {
            if (!Directory.Exists(Util.logDir()))
            {
                Directory.CreateDirectory(Util.logDir());
            }

            return new LogBase((Path.Combine(Util.logDir(), m_logFilename)), "Region", this, m_verbose);
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
            if (m_startupCommandsFile != "")
            {
                RunCommandScript(m_shutdownCommandsFile);
            }

            m_log.Verbose("SHUTDOWN", "Closing all threads");
            m_log.Verbose("SHUTDOWN", "Killing listener thread");
            m_log.Verbose("SHUTDOWN", "Killing clients");
            // TODO: implement this
            m_log.Verbose("SHUTDOWN", "Closing console and terminating");

            m_sceneManager.Close();

            m_log.Close();
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
            MainLog.Instance.Verbose("COMMANDFILE", "Running " + fileName);
            if (File.Exists(fileName))
            {
                StreamReader readFile = File.OpenText(fileName);
                string currentCommand = "";
                while ((currentCommand = readFile.ReadLine()) != null)
                {
                    if (currentCommand != "")
                    {
                        MainLog.Instance.Verbose("COMMANDFILE", "Running '" + currentCommand + "'");
                        MainLog.Instance.MainLogRunCommand(currentCommand);
                    }
                }
            }
            else
            {
                MainLog.Instance.Error("COMMANDFILE", "Command script missing. Can not run commands");
            }
        }

        /// <summary>
        /// Runs commands issued by the server console from the operator
        /// </summary>
        /// <param name="command">The first argument of the parameter (the command)</param>
        /// <param name="cmdparams">Additional arguments passed to the command</param>
        public void RunCmd(string command, string[] cmdparams)
        {
            string result = "";

            switch (command)
            {
                case "set-time":
                    m_sceneManager.SetCurrentSceneTimePhase(Convert.ToInt32(cmdparams[0]));
                    break;

                case "force-update":
                    Console.WriteLine("Updating all clients");
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

                case "help":
                    m_log.Error("alert - send alert to a designated user or all users.");
                    m_log.Error("  alert [First] [Last] [Message] - send an alert to a user. Case sensitive.");
                    m_log.Error("  alert general [Message] - send an alert to all users.");
                    m_log.Error("backup - trigger a simulator backup");
                    m_log.Error("change-region [name] - sets the region that many of these commands affect.");
                    m_log.Error("command-script [filename] - Execute command in a file.");
                    m_log.Error("debug - debugging commands");
                    m_log.Error("  packet 0..255 - print incoming/outgoing packets (0=off)");
                    m_log.Error("edit-scale [prim name] [x] [y] [z] - resize given prim");
                    m_log.Error("export-map [filename] - save image of world map");
                    m_log.Error("force-update - force an update of prims in the scene");
                    m_log.Error("load-xml [filename] - load prims from XML");
                    m_log.Error("load-xml2 [filename] - load prims from XML using version 2 format");
                    m_log.Error("permissions [true/false] - turn on/off permissions on the scene");
                    m_log.Error("quit - equivalent to shutdown.");
                    m_log.Error("restart - disconnects all clients and restarts the sims in the instance.");
                    m_log.Error("remove-region [name] - remove a region");
                    m_log.Error("save-xml [filename] - save prims to XML");
                    m_log.Error("save-xml2 [filename] - save prims to XML using version 2 format");
                    m_log.Error("script - manually trigger scripts? or script commands?");
                    m_log.Error("set-time [x] - set the current scene time phase");
                    m_log.Error("show uptime - show simulator startup and uptime.");
                    m_log.Error("show users - show info about connected users.");
                    m_log.Error("show modules - shows info aboutloaded modules.");
                    m_log.Error("shutdown - disconnect all clients and shutdown.");
                    m_log.Error("terrain help - show help for terrain commands.");
                    break;

                case "show":
                    if (cmdparams.Length > 0)
                    {
                        Show(cmdparams[0]);
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
                                m_log.Error("loadOffsets <X,Y,Z> = <" + loadOffset.X + "," + loadOffset.Y + "," +
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
                    if (!m_sceneManager.RunTerrainCmdOnCurrentScene(cmdparams, ref result))
                    {
                        m_log.Error(result);
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
                    if (CreateAccount != null)
                    {
                        CreateAccount(cmdparams);
                    }
                    break;

                case "create-region":
                    CreateRegion(new RegionInfo(cmdparams[0], "Regions/" + cmdparams[1]));
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
                            MainLog.Instance.Error("Couldn't set current region to: " + regionName);
                        }
                    }

                    if (m_sceneManager.CurrentScene == null)
                    {
                        MainLog.Instance.Verbose("CONSOLE",
                                                 "Currently at Root level. To change region please use 'change-region <regioname>'");
                    }
                    else
                    {
                        MainLog.Instance.Verbose("CONSOLE",
                                                 "Current Region: " + m_sceneManager.CurrentScene.RegionInfo.RegionName +
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

                default:
                    m_log.Error("Unknown command");
                    break;
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
                            m_sceneManager.SetDebugPacketOnCurrentScene(m_log, newDebug);
                        }
                        else
                        {
                            m_log.Error("packet debug should be 0..2");
                        }
                        Console.WriteLine("New packet debug: " + newDebug.ToString());
                    }

                    break;
                default:
                    m_log.Error("Unknown debug");
                    break;
            }
        }

        /// <summary>
        /// Outputs to the console information about the region
        /// </summary>
        /// <param name="ShowWhat">What information to display (valid arguments are "uptime", "users")</param>
        public void Show(string ShowWhat)
        {
            switch (ShowWhat)
            {
                case "uptime":
                    m_log.Error("OpenSim has been running since " + m_startuptime.ToString());
                    m_log.Error("That is " + (DateTime.Now - m_startuptime).ToString());
                    break;
                case "users":
                    m_log.Error(
                        String.Format("{0,-16}{1,-16}{2,-25}{3,-25}{4,-16}{5,-16}{6,-16}", "Firstname", "Lastname",
                                      "Agent ID", "Session ID", "Circuit", "IP", "World"));

                    foreach (ScenePresence presence in m_sceneManager.GetCurrentSceneAvatars())
                    {
                        RegionInfo regionInfo = m_sceneManager.GetRegionInfo(presence.RegionHandle);
                        string regionName;

                        if (regionInfo == null)
                        {
                            regionName = "Unresolvable";
                        }
                        else
                        {
                            regionName = regionInfo.RegionName;
                        }

                        m_log.Error(
                            String.Format("{0,-16}{1,-16}{2,-25}{3,-25}{4,-16},{5,-16}{6,-16}",
                                          presence.Firstname,
                                          presence.Lastname,
                                          presence.UUID,
                                          presence.ControllingClient.AgentId,
                                          "Unknown",
                                          "Unknown",
                                          regionName));
                    }

                    break;
                case "modules":
                    m_log.Error("The currently loaded shared modules are:");
                    foreach (IRegionModule module in m_moduleLoader.GetLoadedSharedModules)
                    {
                        m_log.Error("Shared Module: " + module.Name);
                    }
                    break;

                case "regions":
                    m_sceneManager.ForEachScene(
                        delegate(Scene scene)
                            {
                                m_log.Error("Region Name: " + scene.RegionInfo.RegionName + " , Region XLoc: " +
                                            scene.RegionInfo.RegionLocX + " , Region YLoc: " +
                                            scene.RegionInfo.RegionLocY);
                            });
                    break;
            }
        }

        private string CombineParams(string[] commandParams, int pos)
        {
            string result = "";
            for (int i = pos; i < commandParams.Length; i++)
            {
                result += commandParams[i] + " ";
            }
            result = result.TrimEnd(' ');
            return result;
        }

        #endregion
    }
}