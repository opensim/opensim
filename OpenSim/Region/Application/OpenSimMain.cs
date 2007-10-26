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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
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
using Nini.Config;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Console;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Types;
using OpenSim.Framework.Utilities;
using OpenSim.Region.ClientStack;
using OpenSim.Region.Communications.Local;
using OpenSim.Region.Communications.OGS1;
using OpenSim.Region.Environment;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Physics.Manager;
using OpenSim.Framework.Configuration;
using System.Globalization;
using RegionInfo = OpenSim.Framework.Types.RegionInfo;

namespace OpenSim
{
    public delegate void ConsoleCommand(string[] comParams);

    public class OpenSimMain : RegionApplicationBase, conscmd_callback
    {
        private const string DEFAULT_PRIM_BACKUP_FILENAME = "prim-backup.xml";

        public string m_physicsEngine;
        public string m_scriptEngine;
        public bool m_sandbox;
        public bool user_accounts;
        public bool m_gridLocalAsset;

        private OpenSimController m_controller;

        protected ModuleLoader m_moduleLoader;
        protected LocalLoginService m_loginService;
        private IniConfigSource m_config;

        protected string m_storageDLL = "OpenSim.DataStore.NullStorage.dll";

        protected string m_startupCommandsFile = "";
        protected string m_shutdownCommandsFile = "";

        protected List<UDPServer> m_udpServers = new List<UDPServer>();
        protected List<RegionInfo> m_regionData = new List<RegionInfo>();

        private bool m_verbose;
        private readonly string m_logFilename = ("region-console.log");
        private bool m_permissions = false;

        private bool m_standaloneAuthenticate = false;
        private string m_standaloneWelcomeMessage = null;
        private string m_standaloneInventoryPlugin = "OpenSim.Framework.Data.SQLite.dll";
        private string m_standaloneAssetPlugin = "OpenSim.Framework.Data.SQLite.dll";
        private string m_standaloneUserPlugin = "OpenSim.Framework.Data.DB4o.dll";

        private string m_assetStorage = "db4o";

        public ConsoleCommand CreateAccount = null;
        private bool m_dumpAssetsToFile;

        public OpenSimMain(IConfigSource configSource)
            : base()
        {
            IConfig startupConfig = configSource.Configs["Startup"];

            string iniFile = startupConfig.GetString("inifile", "OpenSim.ini");
            string useExecutePathString = startupConfig.GetString("useexecutepath", "false").ToLower();

            bool useExecutePath = false;
            if (useExecutePathString == "true" || useExecutePathString == "" || useExecutePathString == "1" || useExecutePathString == "yes")
            {
                useExecutePath = true;
            }

            Util.changeUseExecutePathSetting(useExecutePath);

            m_config = new IniConfigSource();
            //check for .INI file (either default or name passed in command line)
            string iniFilePath = Path.Combine(Util.configDir(), iniFile);
            if (File.Exists(iniFilePath))
            {
                m_config.Merge(new IniConfigSource(iniFilePath));
                m_config.Merge(configSource);
            }
            else
            {
                // no default config files, so set default values, and save it
                SetDefaultConfig();

                m_config.Merge(configSource);

                m_config.Save(iniFilePath);
            }

            ReadConfigSettings();

        }

        protected void SetDefaultConfig()
        {
            if (m_config.Configs["Startup"] == null)
                m_config.AddConfig("Startup");
            IConfig config = m_config.Configs["Startup"];
            if (config != null)
            {
                config.Set("gridmode", false);
                config.Set("physics", "basicphysics");
                config.Set("verbose", true);
                config.Set("serverside_object_permissions", false);

                config.Set("storage_plugin", "OpenSim.DataStore.NullStorage.dll");

                config.Set("startup_console_commands_file", "");
                config.Set("shutdown_console_commands_file", "");

                config.Set("script_engine", "DotNetEngine");

                config.Set("asset_database", "sqlite");

                // wtf?
                config.Set("default_modules", true);
                config.Set("default_shared_modules", true);
                config.Set("except_modules", "");
                config.Set("except_shared_modules", "");
            }

            if (m_config.Configs["StandAlone"] == null)
                m_config.AddConfig("StandAlone");

            config = m_config.Configs["StandAlone"];
            if (config != null)
            {
                config.Set("accounts_authenticate", false);
                config.Set("welcome_message", "Welcome to OpenSim");
                config.Set("inventory_plugin", "OpenSim.Framework.Data.SQLite.dll");
                config.Set("userDatabase_plugin", "OpenSim.Framework.Data.SQLite.dll");
                config.Set("asset_plugin", "OpenSim.Framework.Data.SQLite.dll");
                config.Set("dump_assets_to_file", false);
            }

            if (m_config.Configs["Network"] == null)
                m_config.AddConfig("Network");
            config = m_config.Configs["Network"];
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
            }
        }

        protected void ReadConfigSettings()
        {
            m_networkServersInfo = new NetworkServersInfo();

            IConfig startupConfig = m_config.Configs["Startup"];
            if (startupConfig != null)
            {
                m_sandbox = !startupConfig.GetBoolean("gridmode", false);
                m_physicsEngine = startupConfig.GetString("physics", "basicphysics");
                m_verbose = startupConfig.GetBoolean("verbose", true);
                m_permissions = startupConfig.GetBoolean("serverside_object_permissions", false);

                m_storageDLL = startupConfig.GetString("storage_plugin", "OpenSim.DataStore.NullStorage.dll");

                m_startupCommandsFile = startupConfig.GetString("startup_console_commands_file", "");
                m_shutdownCommandsFile = startupConfig.GetString("shutdown_console_commands_file", "");

                m_scriptEngine = startupConfig.GetString("script_engine", "DotNetEngine");

                m_assetStorage = startupConfig.GetString("asset_database", "db4o");
            }

            IConfig standaloneConfig = m_config.Configs["StandAlone"];
            if (standaloneConfig != null)
            {
                m_standaloneAuthenticate = standaloneConfig.GetBoolean("accounts_authenticate", false);
                m_standaloneWelcomeMessage = standaloneConfig.GetString("welcome_message", "Welcome to OpenSim");
                m_standaloneInventoryPlugin =
                    standaloneConfig.GetString("inventory_plugin", "OpenSim.Framework.Data.SQLite.dll");
                m_standaloneUserPlugin =
                    standaloneConfig.GetString("userDatabase_plugin", "OpenSim.Framework.Data.DB4o.dll");
                m_standaloneAssetPlugin = standaloneConfig.GetString("asset_plugin", "OpenSim.Framework.Data.SQLite.dll");

                m_dumpAssetsToFile = standaloneConfig.GetBoolean("dump_assets_to_file", false);
            }

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

            m_controller = new OpenSimController(this, m_httpServer);

            if (m_sandbox)
            {
                LocalInventoryService inventoryService = new LocalInventoryService();
                inventoryService.AddPlugin(m_standaloneInventoryPlugin);

                LocalUserServices userService = new LocalUserServices(m_networkServersInfo, m_networkServersInfo.DefaultHomeLocX, m_networkServersInfo.DefaultHomeLocY, inventoryService);
                userService.AddPlugin(m_standaloneUserPlugin);

                LocalBackEndServices backendService = new LocalBackEndServices();

                CommunicationsLocal localComms = new CommunicationsLocal(m_networkServersInfo, m_httpServer, m_assetCache, userService, inventoryService, backendService, backendService, m_dumpAssetsToFile);
                m_commsManager = localComms;

                m_loginService = new LocalLoginService(userService, m_standaloneWelcomeMessage, localComms, m_networkServersInfo, m_standaloneAuthenticate);
                m_loginService.OnLoginToRegion += backendService.AddNewSession;

                m_httpServer.AddXmlRPCHandler("login_to_simulator", m_loginService.XmlRpcLoginMethod);

                if (m_standaloneAuthenticate)
                {
                    this.CreateAccount = localComms.doCreate;
                }
            }
            else
            {
                m_commsManager = new CommunicationsOGS1(m_networkServersInfo, m_httpServer, m_assetCache);
                m_httpServer.AddStreamHandler(new SimStatusHandler());
            }

            string regionConfigPath = Path.Combine(Util.configDir(), "Regions");

            if (!Directory.Exists(regionConfigPath))
            {
                Directory.CreateDirectory(regionConfigPath);
            }

            string[] configFiles = Directory.GetFiles(regionConfigPath, "*.xml");

            if (configFiles.Length == 0)
            {
                CreateDefaultRegionInfoXml(Path.Combine(regionConfigPath, "default.xml"));
                configFiles = Directory.GetFiles(regionConfigPath, "*.xml");
            }

            m_moduleLoader = new ModuleLoader(m_log, m_config);
            MainLog.Instance.Verbose("Loading Shared Modules");
            m_moduleLoader.LoadDefaultSharedModules();

            // Load all script engines found (scripting engine is now a IRegionModule so loaded in the module loader
            // OpenSim.Region.Environment.Scenes.Scripting.ScriptEngineLoader ScriptEngineLoader = new OpenSim.Region.Environment.Scenes.Scripting.ScriptEngineLoader(m_log);

            for (int i = 0; i < configFiles.Length; i++)
            {
                //Console.WriteLine("Loading region config file");
                RegionInfo regionInfo = new RegionInfo("REGION CONFIG #" + (i + 1), configFiles[i]);

                CreateRegion(regionInfo);
            }

            m_moduleLoader.PostInitialise();
            m_moduleLoader.ClearCache();

            // Start UDP servers
            for (int i = 0; i < m_udpServers.Count; i++)
            {
                this.m_udpServers[i].ServerListener();
            }

            //Run Startup Commands
            if (m_startupCommandsFile != "")
            {
                RunCommandScript(m_startupCommandsFile);
            }
            else
            {
                MainLog.Instance.Verbose("STARTUP", "No startup command script specified. Moving on...");
            }

            MainLog.Instance.Status("STARTUP", "Startup complete, serving " + m_udpServers.Count.ToString() + " region(s)");
        }

        public UDPServer CreateRegion(RegionInfo regionInfo)
        {
            UDPServer udpServer;
            Scene scene = SetupScene(regionInfo, out udpServer);

            m_moduleLoader.InitialiseSharedModules(scene);
            MainLog.Instance.Verbose("MODULES", "Loading Region's Modules");

            m_moduleLoader.PickupModules(scene, ".");
            m_moduleLoader.PickupModules(scene, "ScriptEngines");

            scene.SetModuleInterfaces();

            // Check if we have a script engine to load
            //if (m_scriptEngine != null && m_scriptEngine != "")
            //{
            //  OpenSim.Region.Environment.Scenes.Scripting.ScriptEngineInterface ScriptEngine = ScriptEngineLoader.LoadScriptEngine(m_scriptEngine);
            // scene.AddScriptEngine(ScriptEngine, m_log);
            //}

            //Server side object editing permissions checking
            scene.PermissionsMngr.BypassPermissions = !m_permissions;

            m_sceneManager.Add(scene);

            m_udpServers.Add(udpServer);
            m_regionData.Add(regionInfo);

            return udpServer;
        }

        private static void CreateDefaultRegionInfoXml(string fileName)
        {
            new RegionInfo("DEFAULT REGION CONFIG", fileName);
        }

        protected override StorageManager CreateStorageManager(RegionInfo regionInfo)
        {
            return new StorageManager(m_storageDLL, regionInfo.DataStore, regionInfo.RegionName);
        }

        protected override Scene CreateScene(RegionInfo regionInfo, StorageManager storageManager, AgentCircuitManager circuitManager)
        {
            return new Scene(regionInfo, circuitManager, m_commsManager, m_assetCache, storageManager, m_httpServer, m_moduleLoader, m_dumpAssetsToFile);
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
            else
            {
                SQLAssetServer sqlAssetServer = new SQLAssetServer(m_standaloneAssetPlugin);
                sqlAssetServer.LoadDefaultAssets();
                assetServer = sqlAssetServer;
            }

            m_assetCache = new AssetCache(assetServer);
            // m_assetCache = new assetCache("OpenSim.Region.GridInterfaces.Local.dll", m_networkServersInfo.AssetURL, m_networkServersInfo.AssetSendKey);
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
            return GetPhysicsScene(m_physicsEngine);
        }

        private class SimStatusHandler : IStreamHandler
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

        protected void ConnectToRemoteGridServer()
        {

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
            // IMPLEMENT THIS
            m_log.Verbose("SHUTDOWN", "Closing console and terminating");

            m_sceneManager.Close();

            m_log.Close();
            Environment.Exit(0);
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
                    m_log.Error("command-script [filename] - Execute command in a file.");
                    m_log.Error("debug - debugging commands");
                    m_log.Error("  packet 0..255 - print incoming/outgoing packets (0=off)");
                    m_log.Error("force-update - force an update of prims in the scene");
                    m_log.Error("load-xml [filename] - load prims from XML");
                    m_log.Error("save-xml [filename] - save prims to XML");
                    m_log.Error("script - manually trigger scripts? or script commands?");
                    m_log.Error("show uptime - show simulator startup and uptime.");
                    m_log.Error("show users - show info about connected users.");
                    m_log.Error("show modules - shows info aboutloaded modules.");
                    m_log.Error("shutdown - disconnect all clients and shutdown.");
                    m_log.Error("terrain help - show help for terrain commands.");
                    m_log.Error("quit - equivalent to shutdown.");
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
                    if (cmdparams.Length > 0)
                    {
                        m_sceneManager.LoadCurrentSceneFromXml(cmdparams[0]);
                    }
                    else
                    {
                        m_sceneManager.LoadCurrentSceneFromXml(DEFAULT_PRIM_BACKUP_FILENAME);
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
                    CreateRegion(new RegionInfo(cmdparams[0], "Regions/" + cmdparams[1])).ServerListener();
                    break;

                case "quit":
                case "shutdown":
                    Shutdown();
                    break;

                case "change-region":
                    if (cmdparams.Length > 0)
                    {
                        string regionName = this.CombineParams(cmdparams, 0);

                        if (m_sceneManager.TrySetCurrentScene(regionName))
                        {

                        }
                        else
                        {
                            MainLog.Instance.Error("Couldn't set current region to: " + regionName);
                        }
                    }

                    if (m_sceneManager.CurrentScene == null)
                    {
                        MainLog.Instance.Verbose("Currently at Root level. To change region please use 'change-region <regioname>'");
                    }
                    else
                    {
                        MainLog.Instance.Verbose("Current Region: " + m_sceneManager.CurrentScene.RegionInfo.RegionName + ". To change region please use 'change-region <regioname>'");
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
                        System.Console.WriteLine("New packet debug: " + newDebug.ToString());

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
                    m_log.Error(String.Format("{0,-16}{1,-16}{2,-25}{3,-25}{4,-16}{5,-16}{6,-16}", "Firstname", "Lastname", "Agent ID", "Session ID", "Circuit", "IP", "World"));

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
                    foreach (OpenSim.Region.Environment.Interfaces.IRegionModule module in m_moduleLoader.LoadedSharedModules.Values)
                    {
                        m_log.Error("Shared Module: " + module.Name);
                    }
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
