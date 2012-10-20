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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Timers;
using log4net;
using NDesk.Options;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Monitoring;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim
{
    /// <summary>
    /// Interactive OpenSim region server
    /// </summary>
    public class OpenSim : OpenSimBase
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected string m_startupCommandsFile;
        protected string m_shutdownCommandsFile;
        protected bool m_gui = false;
        protected string m_consoleType = "local";
        protected uint m_consolePort = 0;

        /// <summary>
        /// Prompt to use for simulator command line.
        /// </summary>
        private string m_consolePrompt;

        /// <summary>
        /// Regex for parsing out special characters in the prompt.
        /// </summary>
        private Regex m_consolePromptRegex = new Regex(@"([^\\])\\(\w)", RegexOptions.Compiled);

        private string m_timedScript = "disabled";
        private int m_timeInterval = 1200;
        private Timer m_scriptTimer;

        public OpenSim(IConfigSource configSource) : base(configSource)
        {
        }

        protected override void ReadExtraConfigSettings()
        {
            base.ReadExtraConfigSettings();

            IConfig startupConfig = m_config.Source.Configs["Startup"];
            IConfig networkConfig = m_config.Source.Configs["Network"];

            int stpMaxThreads = 15;

            if (startupConfig != null)
            {
                m_startupCommandsFile = startupConfig.GetString("startup_console_commands_file", "startup_commands.txt");
                m_shutdownCommandsFile = startupConfig.GetString("shutdown_console_commands_file", "shutdown_commands.txt");

                if (startupConfig.GetString("console", String.Empty) == String.Empty)
                    m_gui = startupConfig.GetBoolean("gui", false);
                else
                    m_consoleType= startupConfig.GetString("console", String.Empty);

                if (networkConfig != null)
                    m_consolePort = (uint)networkConfig.GetInt("console_port", 0);

                m_timedScript = startupConfig.GetString("timer_Script", "disabled");
                if (m_timedScript != "disabled")
                {
                    m_timeInterval = startupConfig.GetInt("timer_Interval", 1200);
                }

                if (m_logFileAppender != null)
                {
                    if (m_logFileAppender is log4net.Appender.FileAppender)
                    {
                        log4net.Appender.FileAppender appender =
                                (log4net.Appender.FileAppender)m_logFileAppender;
                        string fileName = startupConfig.GetString("LogFile", String.Empty);
                        if (fileName != String.Empty)
                        {
                            appender.File = fileName;
                            appender.ActivateOptions();
                        }
                        m_log.InfoFormat("[LOGGING]: Logging started to file {0}", appender.File);
                    }
                }

                string asyncCallMethodStr = startupConfig.GetString("async_call_method", String.Empty);
                FireAndForgetMethod asyncCallMethod;
                if (!String.IsNullOrEmpty(asyncCallMethodStr) && Utils.EnumTryParse<FireAndForgetMethod>(asyncCallMethodStr, out asyncCallMethod))
                    Util.FireAndForgetMethod = asyncCallMethod;

                stpMaxThreads = startupConfig.GetInt("MaxPoolThreads", 15);
                m_consolePrompt = startupConfig.GetString("ConsolePrompt", @"Region (\R) ");
            }

            if (Util.FireAndForgetMethod == FireAndForgetMethod.SmartThreadPool)
                Util.InitThreadPool(stpMaxThreads);

            m_log.Info("[OPENSIM MAIN]: Using async_call_method " + Util.FireAndForgetMethod);
        }

        /// <summary>
        /// Performs initialisation of the scene, such as loading configuration from disk.
        /// </summary>
        protected override void StartupSpecific()
        {
            m_log.Info("====================================================================");
            m_log.Info("========================= STARTING OPENSIM =========================");
            m_log.Info("====================================================================");

            //m_log.InfoFormat("[OPENSIM MAIN]: GC Is Server GC: {0}", GCSettings.IsServerGC.ToString());
            // http://msdn.microsoft.com/en-us/library/bb384202.aspx
            //GCSettings.LatencyMode = GCLatencyMode.Batch;
            //m_log.InfoFormat("[OPENSIM MAIN]: GC Latency Mode: {0}", GCSettings.LatencyMode.ToString());

            if (m_gui) // Driven by external GUI
            {
                m_console = new CommandConsole("Region");
            }
            else
            {
                switch (m_consoleType)
                {
                case "basic":
                    m_console = new CommandConsole("Region");
                    break;
                case "rest":
                    m_console = new RemoteConsole("Region");
                    ((RemoteConsole)m_console).ReadConfig(m_config.Source);
                    break;
                default:
                    m_console = new LocalConsole("Region");
                    break;
                }
            }

            MainConsole.Instance = m_console;

            RegisterConsoleCommands();

            base.StartupSpecific();

            MainServer.Instance.AddStreamHandler(new OpenSim.SimStatusHandler());
            MainServer.Instance.AddStreamHandler(new OpenSim.XSimStatusHandler(this));
            if (userStatsURI != String.Empty)
                MainServer.Instance.AddStreamHandler(new OpenSim.UXSimStatusHandler(this));

            if (m_console is RemoteConsole)
            {
                if (m_consolePort == 0)
                {
                    ((RemoteConsole)m_console).SetServer(m_httpServer);
                }
                else
                {
                    ((RemoteConsole)m_console).SetServer(MainServer.GetHttpServer(m_consolePort));
                }
            }

            // Hook up to the watchdog timer
            Watchdog.OnWatchdogTimeout += WatchdogTimeoutHandler;

            PrintFileToConsole("startuplogo.txt");

            // For now, start at the 'root' level by default
            if (SceneManager.Scenes.Count == 1) // If there is only one region, select it
                ChangeSelectedRegion("region",
                                     new string[] {"change", "region", SceneManager.Scenes[0].RegionInfo.RegionName});
            else
                ChangeSelectedRegion("region", new string[] {"change", "region", "root"});

            //Run Startup Commands
            if (String.IsNullOrEmpty(m_startupCommandsFile))
            {
                m_log.Info("[STARTUP]: No startup command script specified. Moving on...");
            }
            else
            {
                RunCommandScript(m_startupCommandsFile);
            }

            // Start timer script (run a script every xx seconds)
            if (m_timedScript != "disabled")
            {
                m_scriptTimer = new Timer();
                m_scriptTimer.Enabled = true;
                m_scriptTimer.Interval = m_timeInterval*1000;
                m_scriptTimer.Elapsed += RunAutoTimerScript;
            }
        }

        /// <summary>
        /// Register standard set of region console commands
        /// </summary>
        private void RegisterConsoleCommands()
        {
            MainServer.RegisterHttpConsoleCommands(m_console);

            m_console.Commands.AddCommand("Objects", false, "force update",
                                          "force update",
                                          "Force the update of all objects on clients",
                                          HandleForceUpdate);

            m_console.Commands.AddCommand("Debug", false, "debug packet",
                                          "debug packet <level> [<avatar-first-name> <avatar-last-name>]",
                                          "Turn on packet debugging",
                                            "If level >  255 then all incoming and outgoing packets are logged.\n"
                                          + "If level <= 255 then incoming AgentUpdate and outgoing SimStats and SimulatorViewerTimeMessage packets are not logged.\n"
                                          + "If level <= 200 then incoming RequestImage and outgoing ImagePacket, ImageData, LayerData and CoarseLocationUpdate packets are not logged.\n"
                                          + "If level <= 100 then incoming ViewerEffect and AgentAnimation and outgoing ViewerEffect and AvatarAnimation packets are not logged.\n"
                                          + "If level <=  50 then outgoing ImprovedTerseObjectUpdate packets are not logged.\n"
                                          + "If level <= 0 then no packets are logged.\n"
                                          + "If an avatar name is given then only packets from that avatar are logged",
                                          Debug);

            m_console.Commands.AddCommand("Debug", false, "debug teleport", "debug teleport", "Toggle teleport route debugging", Debug);

            m_console.Commands.AddCommand("Debug", false, "debug scene",
                                          "debug scene active|collisions|physics|scripting|teleport true|false",
                                          "Turn on scene debugging.",
                                            "If active     is false then main scene update and maintenance loops are suspended.\n"
                                          + "If collisions is false then collisions with other objects are turned off.\n"
                                          + "If physics    is false then all physics objects are non-physical.\n"
                                          + "If scripting  is false then no scripting operations happen.\n"
                                          + "If teleport   is true  then some extra teleport debug information is logged.",
                                          Debug);

            m_console.Commands.AddCommand("General", false, "change region",
                                          "change region <region name>",
                                          "Change current console region", ChangeSelectedRegion);

            m_console.Commands.AddCommand("Archiving", false, "save xml",
                                          "save xml",
                                          "Save a region's data in XML format", SaveXml);

            m_console.Commands.AddCommand("Archiving", false, "save xml2",
                                          "save xml2",
                                          "Save a region's data in XML2 format", SaveXml2);

            m_console.Commands.AddCommand("Archiving", false, "load xml",
                                          "load xml [-newIDs [<x> <y> <z>]]",
                                          "Load a region's data from XML format", LoadXml);

            m_console.Commands.AddCommand("Archiving", false, "load xml2",
                                          "load xml2",
                                          "Load a region's data from XML2 format", LoadXml2);

            m_console.Commands.AddCommand("Archiving", false, "save prims xml2",
                                          "save prims xml2 [<prim name> <file name>]",
                                          "Save named prim to XML2", SavePrimsXml2);

            m_console.Commands.AddCommand("Archiving", false, "load oar",
                                          "load oar [--merge] [--skip-assets] [<OAR path>]",
                                          "Load a region's data from an OAR archive.",
                                          "--merge will merge the OAR with the existing scene." + Environment.NewLine
                                          + "--skip-assets will load the OAR but ignore the assets it contains." + Environment.NewLine
                                          + "The path can be either a filesystem location or a URI."
                                          + "  If this is not given then the command looks for an OAR named region.oar in the current directory.",
                                          LoadOar);

            m_console.Commands.AddCommand("Archiving", false, "save oar",
                                          //"save oar [-v|--version=<N>] [-p|--profile=<url>] [<OAR path>]",
                                          "save oar [-h|--home=<url>] [--noassets] [--publish] [--perm=<permissions>] [--all] [<OAR path>]",
                                          "Save a region's data to an OAR archive.",
//                                          "-v|--version=<N> generates scene objects as per older versions of the serialization (e.g. -v=0)" + Environment.NewLine
                                          "-h|--home=<url> adds the url of the profile service to the saved user information.\n"
                                          + "--noassets stops assets being saved to the OAR.\n"
                                          + "--publish saves an OAR stripped of owner and last owner information.\n"
                                          + "   on reload, the estate owner will be the owner of all objects\n"
                                          + "   this is useful if you're making oars generally available that might be reloaded to the same grid from which you published\n"
                                          + "--perm=<permissions> stops objects with insufficient permissions from being saved to the OAR.\n"
                                          + "   <permissions> can contain one or more of these characters: \"C\" = Copy, \"T\" = Transfer\n"
                                          + "--all saves all the regions in the simulator, instead of just the current region.\n"
                                          + "The OAR path must be a filesystem path."
                                          + " If this is not given then the oar is saved to region.oar in the current directory.",
                                          SaveOar);

            m_console.Commands.AddCommand("Objects", false, "edit scale",
                                          "edit scale <name> <x> <y> <z>",
                                          "Change the scale of a named prim", HandleEditScale);

            m_console.Commands.AddCommand("Users", false, "kick user",
                                          "kick user <first> <last> [--force] [message]",
                                          "Kick a user off the simulator",
                                          "The --force option will kick the user without any checks to see whether it's already in the process of closing\n"
                                          + "Only use this option if you are sure the avatar is inactive and a normal kick user operation does not removed them",
                                          KickUserCommand);

            m_console.Commands.AddCommand("Users", false, "show users",
                                          "show users [full]",
                                          "Show user data for users currently on the region", 
                                          "Without the 'full' option, only users actually on the region are shown."
                                            + "  With the 'full' option child agents of users in neighbouring regions are also shown.",
                                          HandleShow);

            m_console.Commands.AddCommand("Comms", false, "show connections",
                                          "show connections",
                                          "Show connection data", HandleShow);

            m_console.Commands.AddCommand("Comms", false, "show circuits",
                                          "show circuits",
                                          "Show agent circuit data", HandleShow);

            m_console.Commands.AddCommand("Comms", false, "show pending-objects",
                                          "show pending-objects",
                                          "Show # of objects on the pending queues of all scene viewers", HandleShow);

            m_console.Commands.AddCommand("General", false, "show modules",
                                          "show modules",
                                          "Show module data", HandleShow);

            m_console.Commands.AddCommand("Regions", false, "show regions",
                                          "show regions",
                                          "Show region data", HandleShow);
            
            m_console.Commands.AddCommand("Regions", false, "show ratings",
                                          "show ratings",
                                          "Show rating data", HandleShow);

            m_console.Commands.AddCommand("Objects", false, "backup",
                                          "backup",
                                          "Persist currently unsaved object changes immediately instead of waiting for the normal persistence call.", RunCommand);

            m_console.Commands.AddCommand("Regions", false, "create region",
                                          "create region [\"region name\"] <region_file.ini>",
                                          "Create a new region.",
                                          "The settings for \"region name\" are read from <region_file.ini>. Paths specified with <region_file.ini> are relative to your Regions directory, unless an absolute path is given."
                                          + " If \"region name\" does not exist in <region_file.ini>, it will be added." + Environment.NewLine
                                          + "Without \"region name\", the first region found in <region_file.ini> will be created." + Environment.NewLine
                                          + "If <region_file.ini> does not exist, it will be created.",
                                          HandleCreateRegion);

            m_console.Commands.AddCommand("Regions", false, "restart",
                                          "restart",
                                          "Restart all sims in this instance", RunCommand);

            m_console.Commands.AddCommand("General", false, "config set",
                                          "config set <section> <key> <value>",
                                          "Set a config option.  In most cases this is not useful since changed parameters are not dynamically reloaded.  Neither do changed parameters persist - you will have to change a config file manually and restart.", HandleConfig);

            m_console.Commands.AddCommand("General", false, "config get",
                                          "config get [<section>] [<key>]",
                                          "Synonym for config show",
                                          HandleConfig);
            
            m_console.Commands.AddCommand("General", false, "config show",
                                          "config show [<section>] [<key>]",
                                          "Show config information", 
                                          "If neither section nor field are specified, then the whole current configuration is printed." + Environment.NewLine
                                          + "If a section is given but not a field, then all fields in that section are printed.",
                                          HandleConfig);            

            m_console.Commands.AddCommand("General", false, "config save",
                                          "config save <path>",
                                          "Save current configuration to a file at the given path", HandleConfig);

            m_console.Commands.AddCommand("General", false, "command-script",
                                          "command-script <script>",
                                          "Run a command script from file", RunCommand);

            m_console.Commands.AddCommand("Regions", false, "remove-region",
                                          "remove-region <name>",
                                          "Remove a region from this simulator", RunCommand);

            m_console.Commands.AddCommand("Regions", false, "delete-region",
                                          "delete-region <name>",
                                          "Delete a region from disk", RunCommand);

            m_console.Commands.AddCommand("General", false, "modules list",
                                          "modules list",
                                          "List modules", HandleModules);

            m_console.Commands.AddCommand("General", false, "modules load",
                                          "modules load <name>",
                                          "Load a module", HandleModules);

            m_console.Commands.AddCommand("General", false, "modules unload",
                                          "modules unload <name>",
                                          "Unload a module", HandleModules);
        }

        public override void ShutdownSpecific()
        {
            if (m_shutdownCommandsFile != String.Empty)
            {
                RunCommandScript(m_shutdownCommandsFile);
            }
            
            base.ShutdownSpecific();
        }

        /// <summary>
        /// Timer to run a specific text file as console commands.  Configured in in the main ini file
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RunAutoTimerScript(object sender, EventArgs e)
        {
            if (m_timedScript != "disabled")
            {
                RunCommandScript(m_timedScript);
            }
        }

        private void WatchdogTimeoutHandler(Watchdog.ThreadWatchdogInfo twi)
        {
            int now = Environment.TickCount & Int32.MaxValue;

            m_log.ErrorFormat(
                "[WATCHDOG]: Timeout detected for thread \"{0}\". ThreadState={1}. Last tick was {2}ms ago.  {3}",
                twi.Thread.Name,
                twi.Thread.ThreadState,
                now - twi.LastTick,
                twi.AlarmMethod != null ? string.Format("Data: {0}", twi.AlarmMethod()) : "");
        }

        #region Console Commands

        /// <summary>
        /// Kicks users off the region
        /// </summary>
        /// <param name="module"></param>
        /// <param name="cmdparams">name of avatar to kick</param>
        private void KickUserCommand(string module, string[] cmdparams)
        {
            bool force = false;
            
            OptionSet options = new OptionSet().Add("f|force", delegate (string v) { force = v != null; });

            List<string> mainParams = options.Parse(cmdparams);

            if (mainParams.Count < 4)
                return;

            string alert = null;
            if (mainParams.Count > 4)
                alert = String.Format("\n{0}\n", String.Join(" ", cmdparams, 4, cmdparams.Length - 4));

            IList agents = SceneManager.GetCurrentSceneAvatars();

            foreach (ScenePresence presence in agents)
            {
                RegionInfo regionInfo = presence.Scene.RegionInfo;

                if (presence.Firstname.ToLower().Contains(mainParams[2].ToLower()) &&
                    presence.Lastname.ToLower().Contains(mainParams[3].ToLower()))
                {
                    MainConsole.Instance.Output(
                        String.Format(
                            "Kicking user: {0,-16} {1,-16} {2,-37} in region: {3,-16}",
                            presence.Firstname, presence.Lastname, presence.UUID, regionInfo.RegionName));

                    // kick client...
                    if (alert != null)
                        presence.ControllingClient.Kick(alert);
                    else
                        presence.ControllingClient.Kick("\nThe OpenSim manager kicked you out.\n");

                    presence.Scene.IncomingCloseAgent(presence.UUID, force);
                }
            }

            MainConsole.Instance.Output("");
        }

        /// <summary>
        /// Run an optional startup list of commands
        /// </summary>
        /// <param name="fileName"></param>
        private void RunCommandScript(string fileName)
        {
            if (File.Exists(fileName))
            {
                m_log.Info("[COMMANDFILE]: Running " + fileName);

                using (StreamReader readFile = File.OpenText(fileName))
                {
                    string currentCommand;
                    while ((currentCommand = readFile.ReadLine()) != null)
                    {
                        currentCommand = currentCommand.Trim();
                        if (!(currentCommand == ""
                            || currentCommand.StartsWith(";")
                            || currentCommand.StartsWith("//")
                            || currentCommand.StartsWith("#")))
                        {
                            m_log.Info("[COMMANDFILE]: Running '" + currentCommand + "'");
                            m_console.RunCommand(currentCommand);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Opens a file and uses it as input to the console command parser.
        /// </summary>
        /// <param name="fileName">name of file to use as input to the console</param>
        private static void PrintFileToConsole(string fileName)
        {
            if (File.Exists(fileName))
            {
                StreamReader readFile = File.OpenText(fileName);
                string currentLine;
                while ((currentLine = readFile.ReadLine()) != null)
                {
                    m_log.Info("[!]" + currentLine);
                }
            }
        }

        /// <summary>
        /// Force resending of all updates to all clients in active region(s)
        /// </summary>
        /// <param name="module"></param>
        /// <param name="args"></param>
        private void HandleForceUpdate(string module, string[] args)
        {
            MainConsole.Instance.Output("Updating all clients");
            SceneManager.ForceCurrentSceneClientUpdate();
        }

        /// <summary>
        /// Edits the scale of a primative with the name specified
        /// </summary>
        /// <param name="module"></param>
        /// <param name="args">0,1, name, x, y, z</param>
        private void HandleEditScale(string module, string[] args)
        {
            if (args.Length == 6)
            {
                SceneManager.HandleEditCommandOnCurrentScene(args);
            }
            else
            {
                MainConsole.Instance.Output("Argument error: edit scale <prim name> <x> <y> <z>");
            }
        }

        /// <summary>
        /// Creates a new region based on the parameters specified.   This will ask the user questions on the console
        /// </summary>
        /// <param name="module"></param>
        /// <param name="cmd">0,1,region name, region ini or XML file</param>
        private void HandleCreateRegion(string module, string[] cmd)
        {
            string regionName = string.Empty;
            string regionFile = string.Empty;

            if (cmd.Length == 3)
            {
                regionFile = cmd[2];
            }
            else if (cmd.Length > 3)
            {
                regionName = cmd[2];
                regionFile = cmd[3];
            }

            string extension = Path.GetExtension(regionFile).ToLower();
            bool isXml = extension.Equals(".xml");
            bool isIni = extension.Equals(".ini");

            if (!isXml && !isIni)
            {
                MainConsole.Instance.Output("Usage: create region [\"region name\"] <region_file.ini>");
                return;
            }

            if (!Path.IsPathRooted(regionFile))
            {
                string regionsDir = ConfigSource.Source.Configs["Startup"].GetString("regionload_regionsdir", "Regions").Trim();
                regionFile = Path.Combine(regionsDir, regionFile);
            }

            RegionInfo regInfo;
            if (isXml)
            {
                regInfo = new RegionInfo(regionName, regionFile, false, ConfigSource.Source);
            }
            else
            {
                regInfo = new RegionInfo(regionName, regionFile, false, ConfigSource.Source, regionName);
            }

            Scene existingScene;
            if (SceneManager.TryGetScene(regInfo.RegionID, out existingScene))
            {
                MainConsole.Instance.OutputFormat(
                    "ERROR: Cannot create region {0} with ID {1}, this ID is already assigned to region {2}",
                    regInfo.RegionName, regInfo.RegionID, existingScene.RegionInfo.RegionName);

                return;
            }

            bool changed = PopulateRegionEstateInfo(regInfo);
            IScene scene;
            CreateRegion(regInfo, true, out scene);
            if (changed)
	      regInfo.EstateSettings.Save();
        }

        /// <summary>
        /// Change and load configuration file data.
        /// </summary>
        /// <param name="module"></param>
        /// <param name="cmd"></param>
        private void HandleConfig(string module, string[] cmd)
        {
            List<string> args = new List<string>(cmd);
            args.RemoveAt(0);
            string[] cmdparams = args.ToArray();

            if (cmdparams.Length > 0)
            {
                string firstParam = cmdparams[0].ToLower();
                
                switch (firstParam)
                {
                    case "set":
                        if (cmdparams.Length < 4)
                        {
                            Notice("Syntax: config set <section> <key> <value>");
                            Notice("Example: config set ScriptEngine.DotNetEngine NumberOfScriptThreads 5");
                        }
                        else
                        {
                            IConfig c;
                            IConfigSource source = new IniConfigSource();
                            c = source.AddConfig(cmdparams[1]);
                            if (c != null)
                            {
                                string _value = String.Join(" ", cmdparams, 3, cmdparams.Length - 3);
                                c.Set(cmdparams[2], _value);
                                m_config.Source.Merge(source);

                                Notice("In section [{0}], set {1} = {2}", c.Name, cmdparams[2], _value);
                            }
                        }
                        break;

                    case "get":
                    case "show":
                        if (cmdparams.Length == 1)
                        {
                            foreach (IConfig config in m_config.Source.Configs)
                            {
                                Notice("[{0}]", config.Name);
                                string[] keys = config.GetKeys();
                                foreach (string key in keys)
                                    Notice("  {0} = {1}", key, config.GetString(key));
                            }
                        }
                        else if (cmdparams.Length == 2 || cmdparams.Length == 3)
                        {
                            IConfig config = m_config.Source.Configs[cmdparams[1]];
                            if (config == null)
                            {
                                Notice("Section \"{0}\" does not exist.",cmdparams[1]);
                                break;
                            }
                            else
                            {
                                if (cmdparams.Length == 2)
                                {
                                    Notice("[{0}]", config.Name);
                                    foreach (string key in config.GetKeys())
                                        Notice("  {0} = {1}", key, config.GetString(key));                                
                                }
                                else
                                {
                                    Notice(
                                        "config get {0} {1} : {2}", 
                                        cmdparams[1], cmdparams[2], config.GetString(cmdparams[2]));
                                }
                            }
                        }
                        else
                        {
                            Notice("Syntax: config {0} [<section>] [<key>]", firstParam);
                            Notice("Example: config {0} ScriptEngine.DotNetEngine NumberOfScriptThreads", firstParam);
                        }

                        break;

                    case "save":
                        if (cmdparams.Length < 2)
                        {
                            Notice("Syntax: config save <path>");
                            return;
                        }

                        if (Application.iniFilePath == cmdparams[1])
                        {
                            Notice("Path can not be " + Application.iniFilePath);
                            return;
                        }

                        Notice("Saving configuration file: " + cmdparams[1]);
                        m_config.Save(cmdparams[1]);
                        break;
                }
            }
        }

        /// <summary>
        /// Load, Unload, and list Region modules in use
        /// </summary>
        /// <param name="module"></param>
        /// <param name="cmd"></param>
        private void HandleModules(string module, string[] cmd)
        {
            List<string> args = new List<string>(cmd);
            args.RemoveAt(0);
            string[] cmdparams = args.ToArray();

            if (cmdparams.Length > 0)
            {
                switch (cmdparams[0].ToLower())
                {
                    case "list":
                        foreach (IRegionModule irm in m_moduleLoader.GetLoadedSharedModules)
                        {
                            MainConsole.Instance.Output(String.Format("Shared region module: {0}", irm.Name));
                        }
                        break;
                    case "unload":
                        if (cmdparams.Length > 1)
                        {
                            foreach (IRegionModule rm in new ArrayList(m_moduleLoader.GetLoadedSharedModules))
                            {
                                if (rm.Name.ToLower() == cmdparams[1].ToLower())
                                {
                                    MainConsole.Instance.Output(String.Format("Unloading module: {0}", rm.Name));
                                    m_moduleLoader.UnloadModule(rm);
                                }
                            }
                        }
                        break;
                    case "load":
                        if (cmdparams.Length > 1)
                        {
                            foreach (Scene s in new ArrayList(SceneManager.Scenes))
                            {
                                MainConsole.Instance.Output(String.Format("Loading module: {0}", cmdparams[1]));
                                m_moduleLoader.LoadRegionModules(cmdparams[1], s);
                            }
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// Runs commands issued by the server console from the operator
        /// </summary>
        /// <param name="command">The first argument of the parameter (the command)</param>
        /// <param name="cmdparams">Additional arguments passed to the command</param>
        public void RunCommand(string module, string[] cmdparams)
        {
            List<string> args = new List<string>(cmdparams);
            if (args.Count < 1)
                return;

            string command = args[0];
            args.RemoveAt(0);

            cmdparams = args.ToArray();

            switch (command)
            {
                case "command-script":
                    if (cmdparams.Length > 0)
                    {
                        RunCommandScript(cmdparams[0]);
                    }
                    break;

                case "backup":
                    MainConsole.Instance.Output("Triggering save of pending object updates to persistent store");
                    SceneManager.BackupCurrentScene();
                    break;

                case "remove-region":
                    string regRemoveName = CombineParams(cmdparams, 0);

                    Scene removeScene;
                    if (SceneManager.TryGetScene(regRemoveName, out removeScene))
                        RemoveRegion(removeScene, false);
                    else
                        MainConsole.Instance.Output("No region with that name");
                    break;

                case "delete-region":
                    string regDeleteName = CombineParams(cmdparams, 0);

                    Scene killScene;
                    if (SceneManager.TryGetScene(regDeleteName, out killScene))
                        RemoveRegion(killScene, true);
                    else
                        MainConsole.Instance.Output("no region with that name");
                    break;

                case "restart":
                    SceneManager.RestartCurrentScene();
                    break;
            }
        }

        /// <summary>
        /// Change the currently selected region.  The selected region is that operated upon by single region commands.
        /// </summary>
        /// <param name="cmdParams"></param>
        protected void ChangeSelectedRegion(string module, string[] cmdparams)
        {
            if (cmdparams.Length > 2)
            {
                string newRegionName = CombineParams(cmdparams, 2);

                if (!SceneManager.TrySetCurrentScene(newRegionName))
                    MainConsole.Instance.Output(String.Format("Couldn't select region {0}", newRegionName));
            }
            else
            {
                MainConsole.Instance.Output("Usage: change region <region name>");
            }

            string regionName = (SceneManager.CurrentScene == null ? "root" : SceneManager.CurrentScene.RegionInfo.RegionName);
            MainConsole.Instance.Output(String.Format("Currently selected region is {0}", regionName));

//            m_log.DebugFormat("Original prompt is {0}", m_consolePrompt);
            string prompt = m_consolePrompt;

            // Replace "\R" with the region name
            // Replace "\\" with "\"
            prompt = m_consolePromptRegex.Replace(prompt, m =>
            {
//                m_log.DebugFormat("Matched {0}", m.Groups[2].Value);
                if (m.Groups[2].Value == "R")
                    return m.Groups[1].Value + regionName;
                else
                    return m.Groups[0].Value;
            });

            m_console.DefaultPrompt = prompt;
            m_console.ConsoleScene = SceneManager.CurrentScene;
        }

        /// <summary>
        /// Turn on some debugging values for OpenSim.
        /// </summary>
        /// <param name="args"></param>
        protected void Debug(string module, string[] args)
        {
            if (args.Length == 1)
                return;

            switch (args[1])
            {
                case "packet":
                    string name = null;
                    if (args.Length == 5)
                        name = string.Format("{0} {1}", args[3], args[4]);

                    if (args.Length > 2)
                    {
                        int newDebug;
                        if (int.TryParse(args[2], out newDebug))
                        {
                            SceneManager.SetDebugPacketLevelOnCurrentScene(newDebug, name);
                            // We provide user information elsewhere if any clients had their debug level set.
//                            MainConsole.Instance.OutputFormat("Debug packet level set to {0}", newDebug);
                        }
                        else
                        {
                            MainConsole.Instance.Output("Usage: debug packet 0..255");
                        }
                    }

                    break;

                case "scene":
                    if (args.Length == 4)
                    {
                        if (SceneManager.CurrentScene == null)
                        {
                            MainConsole.Instance.Output("Please use 'change region <regioname>' first");
                        }
                        else
                        {
                            string key = args[2];
                            string value = args[3];
                            SceneManager.CurrentScene.SetSceneCoreDebug(
                                new Dictionary<string, string>() { { key, value } });

                            MainConsole.Instance.OutputFormat("Set debug scene {0} = {1}", key, value);
                        }
                    }
                    else
                    {
                        MainConsole.Instance.Output(
                            "Usage: debug scene active|scripting|collisions|physics|teleport true|false");
                    }

                    break;

                default:
                    MainConsole.Instance.Output("Unknown debug command");
                    break;
            }
        }

        // see BaseOpenSimServer
        /// <summary>
        /// Many commands list objects for debugging.  Some of the types are listed  here
        /// </summary>
        /// <param name="mod"></param>
        /// <param name="cmd"></param>
        public override void HandleShow(string mod, string[] cmd)
        {
            base.HandleShow(mod, cmd);

            List<string> args = new List<string>(cmd);
            args.RemoveAt(0);
            string[] showParams = args.ToArray();

            switch (showParams[0])
            {
                case "users":
                    IList agents;
                    if (showParams.Length > 1 && showParams[1] == "full")
                    {
                        agents = SceneManager.GetCurrentScenePresences();
                    } else
                    {
                        agents = SceneManager.GetCurrentSceneAvatars();
                    }
                
                    MainConsole.Instance.Output(String.Format("\nAgents connected: {0}\n", agents.Count));

                    MainConsole.Instance.Output(
                        String.Format("{0,-16} {1,-16} {2,-37} {3,-11} {4,-16} {5,-30}", "Firstname", "Lastname",
                                      "Agent ID", "Root/Child", "Region", "Position")
                    );

                    foreach (ScenePresence presence in agents)
                    {
                        RegionInfo regionInfo = presence.Scene.RegionInfo;
                        string regionName;

                        if (regionInfo == null)
                        {
                            regionName = "Unresolvable";
                        } else
                        {
                            regionName = regionInfo.RegionName;
                        }

                        MainConsole.Instance.Output(
                            String.Format(
                                "{0,-16} {1,-16} {2,-37} {3,-11} {4,-16} {5,-30}",
                                presence.Firstname,
                                presence.Lastname,
                                presence.UUID,
                                presence.IsChildAgent ? "Child" : "Root",
                                regionName,
                                presence.AbsolutePosition.ToString())
                        );
                    }

                    MainConsole.Instance.Output(String.Empty);
                    break;

                case "connections":
                    HandleShowConnections();
                    break;

                case "circuits":
                    HandleShowCircuits();
                    break;

                case "modules":
                    MainConsole.Instance.Output("The currently loaded shared modules are:");
                    foreach (IRegionModule module in m_moduleLoader.GetLoadedSharedModules)
                    {
                        MainConsole.Instance.Output("Shared Module: " + module.Name);
                    }

                    SceneManager.ForEachScene(
                        delegate(Scene scene) {
                        m_log.Error("The currently loaded modules in " + scene.RegionInfo.RegionName + " are:");
                        foreach (IRegionModule module in scene.Modules.Values)
                        {
                            if (!module.IsSharedModule)
                            {
                                m_log.Error("Region Module: " + module.Name);
                            }
                        }
                    }
                    );

                    SceneManager.ForEachScene(
                        delegate(Scene scene) {
                        MainConsole.Instance.Output("Loaded new region modules in" + scene.RegionInfo.RegionName + " are:");
                        foreach (IRegionModuleBase module in scene.RegionModules.Values)
                        {
                            Type type = module.GetType().GetInterface("ISharedRegionModule");
                            string module_type = type != null ? "Shared" : "Non-Shared";
                            MainConsole.Instance.OutputFormat("New Region Module ({0}): {1}", module_type, module.Name);
                        }
                    }
                    );

                    MainConsole.Instance.Output("");
                    break;

                case "regions":
                    SceneManager.ForEachScene(
                        delegate(Scene scene)
                            {
                                MainConsole.Instance.Output(String.Format(
                                           "Region Name: {0}, Region XLoc: {1}, Region YLoc: {2}, Region Port: {3}, Estate Name: {4}",
                                           scene.RegionInfo.RegionName,
                                           scene.RegionInfo.RegionLocX,
                                           scene.RegionInfo.RegionLocY,
                                           scene.RegionInfo.InternalEndPoint.Port,
                                           scene.RegionInfo.EstateSettings.EstateName));
                            });
                    break;

                case "ratings":
                    SceneManager.ForEachScene(
                    delegate(Scene scene)
                    {
                        string rating = "";
                        if (scene.RegionInfo.RegionSettings.Maturity == 1)
                        {
                            rating = "MATURE";
                        }
                        else if (scene.RegionInfo.RegionSettings.Maturity == 2)
                        {
                            rating = "ADULT";
                        }
                        else
                        {
                            rating = "PG";
                        }
                        MainConsole.Instance.Output(String.Format(
                                   "Region Name: {0}, Region Rating {1}",
                                   scene.RegionInfo.RegionName,
                                   rating));
                    });
                    break;
            }
        }

        private void HandleShowCircuits()
        {
            ConsoleDisplayTable cdt = new ConsoleDisplayTable();
            cdt.AddColumn("Region", 20);
            cdt.AddColumn("Avatar name", 24);
            cdt.AddColumn("Type", 5);
            cdt.AddColumn("Code", 10);
            cdt.AddColumn("IP", 16);
            cdt.AddColumn("Viewer Name", 24);

            SceneManager.ForEachScene(
                s =>
                {
                    foreach (AgentCircuitData aCircuit in s.AuthenticateHandler.GetAgentCircuits().Values)
                        cdt.AddRow(
                            s.Name,
                            aCircuit.Name,
                            aCircuit.child ? "child" : "root",
                            aCircuit.circuitcode.ToString(),
                            aCircuit.IPAddress.ToString(),
                            aCircuit.Viewer);
                });

            MainConsole.Instance.Output(cdt.ToString());
        }

        private void HandleShowConnections()
        {
            ConsoleDisplayTable cdt = new ConsoleDisplayTable();
            cdt.AddColumn("Region", 20);
            cdt.AddColumn("Avatar name", 24);
            cdt.AddColumn("Circuit code", 12);
            cdt.AddColumn("Endpoint", 23);
            cdt.AddColumn("Active?", 7);

            SceneManager.ForEachScene(
                s => s.ForEachClient(
                    c => cdt.AddRow(
                        s.Name,
                        c.Name,
                        c.CircuitCode.ToString(),
                        c.RemoteEndPoint.ToString(),                
                        c.IsActive.ToString())));

            MainConsole.Instance.Output(cdt.ToString());
        }

        /// <summary>
        /// Use XML2 format to serialize data to a file
        /// </summary>
        /// <param name="module"></param>
        /// <param name="cmdparams"></param>
        protected void SavePrimsXml2(string module, string[] cmdparams)
        {
            if (cmdparams.Length > 5)
            {
                SceneManager.SaveNamedPrimsToXml2(cmdparams[3], cmdparams[4]);
            }
            else
            {
                SceneManager.SaveNamedPrimsToXml2("Primitive", DEFAULT_PRIM_BACKUP_FILENAME);
            }
        }

        /// <summary>
        /// Use XML format to serialize data to a file
        /// </summary>
        /// <param name="module"></param>
        /// <param name="cmdparams"></param>
        protected void SaveXml(string module, string[] cmdparams)
        {
            MainConsole.Instance.Output("PLEASE NOTE, save-xml is DEPRECATED and may be REMOVED soon.  If you are using this and there is some reason you can't use save-xml2, please file a mantis detailing the reason.");

            if (cmdparams.Length > 0)
            {
                SceneManager.SaveCurrentSceneToXml(cmdparams[2]);
            }
            else
            {
                SceneManager.SaveCurrentSceneToXml(DEFAULT_PRIM_BACKUP_FILENAME);
            }
        }

        /// <summary>
        /// Loads data and region objects from XML format.
        /// </summary>
        /// <param name="module"></param>
        /// <param name="cmdparams"></param>
        protected void LoadXml(string module, string[] cmdparams)
        {
            MainConsole.Instance.Output("PLEASE NOTE, load-xml is DEPRECATED and may be REMOVED soon.  If you are using this and there is some reason you can't use load-xml2, please file a mantis detailing the reason.");

            Vector3 loadOffset = new Vector3(0, 0, 0);
            if (cmdparams.Length > 2)
            {
                bool generateNewIDS = false;
                if (cmdparams.Length > 3)
                {
                    if (cmdparams[3] == "-newUID")
                    {
                        generateNewIDS = true;
                    }
                    if (cmdparams.Length > 4)
                    {
                        loadOffset.X = (float)Convert.ToDecimal(cmdparams[4], Culture.NumberFormatInfo);
                        if (cmdparams.Length > 5)
                        {
                            loadOffset.Y = (float)Convert.ToDecimal(cmdparams[5], Culture.NumberFormatInfo);
                        }
                        if (cmdparams.Length > 6)
                        {
                            loadOffset.Z = (float)Convert.ToDecimal(cmdparams[6], Culture.NumberFormatInfo);
                        }
                        MainConsole.Instance.Output(String.Format("loadOffsets <X,Y,Z> = <{0},{1},{2}>",loadOffset.X,loadOffset.Y,loadOffset.Z));
                    }
                }
                SceneManager.LoadCurrentSceneFromXml(cmdparams[2], generateNewIDS, loadOffset);
            }
            else
            {
                try
                {
                    SceneManager.LoadCurrentSceneFromXml(DEFAULT_PRIM_BACKUP_FILENAME, false, loadOffset);
                }
                catch (FileNotFoundException)
                {
                    MainConsole.Instance.Output("Default xml not found. Usage: load-xml <filename>");
                }
            }
        }
        /// <summary>
        /// Serialize region data to XML2Format
        /// </summary>
        /// <param name="module"></param>
        /// <param name="cmdparams"></param>
        protected void SaveXml2(string module, string[] cmdparams)
        {
            if (cmdparams.Length > 2)
            {
                SceneManager.SaveCurrentSceneToXml2(cmdparams[2]);
            }
            else
            {
                SceneManager.SaveCurrentSceneToXml2(DEFAULT_PRIM_BACKUP_FILENAME);
            }
        }

        /// <summary>
        /// Load region data from Xml2Format
        /// </summary>
        /// <param name="module"></param>
        /// <param name="cmdparams"></param>
        protected void LoadXml2(string module, string[] cmdparams)
        {
            if (cmdparams.Length > 2)
            {
                try
                {
                    SceneManager.LoadCurrentSceneFromXml2(cmdparams[2]);
                }
                catch (FileNotFoundException)
                {
                    MainConsole.Instance.Output("Specified xml not found. Usage: load xml2 <filename>");
                }
            }
            else
            {
                try
                {
                    SceneManager.LoadCurrentSceneFromXml2(DEFAULT_PRIM_BACKUP_FILENAME);
                }
                catch (FileNotFoundException)
                {
                    MainConsole.Instance.Output("Default xml not found. Usage: load xml2 <filename>");
                }
            }
        }

        /// <summary>
        /// Load a whole region from an opensimulator archive.
        /// </summary>
        /// <param name="cmdparams"></param>
        protected void LoadOar(string module, string[] cmdparams)
        {
            try
            {
                SceneManager.LoadArchiveToCurrentScene(cmdparams);
            }
            catch (Exception e)
            {
                MainConsole.Instance.Output(e.Message);
            }
        }

        /// <summary>
        /// Save a region to a file, including all the assets needed to restore it.
        /// </summary>
        /// <param name="cmdparams"></param>
        protected void SaveOar(string module, string[] cmdparams)
        {
            SceneManager.SaveCurrentSceneToArchive(cmdparams);
        }

        private static string CombineParams(string[] commandParams, int pos)
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
    }
}
