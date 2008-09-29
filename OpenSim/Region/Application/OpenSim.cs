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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;
using OpenMetaverse;
using log4net;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Framework.Statistics;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;
using Timer=System.Timers.Timer;

namespace OpenSim
{
    /// <summary>
    /// Interactive OpenSim region server
    /// </summary>
    public class OpenSim : OpenSimBase, conscmd_callback
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected string m_startupCommandsFile;
        protected string m_shutdownCommandsFile;

        private string m_timedScript = "disabled";
        private Timer m_scriptTimer;

        /// <summary>
        /// List of Console Plugin Commands
        /// </summary>
        private static List<ConsolePluginCommand> m_PluginCommandInfos = new List<ConsolePluginCommand>();

        public OpenSim(IConfigSource configSource) : base(configSource)
        {
        }

        protected override void ReadConfigSettings()
        {
            IConfig startupConfig = m_config.Source.Configs["Startup"];

            if (startupConfig != null)
            {
                m_startupCommandsFile = startupConfig.GetString("startup_console_commands_file", String.Empty);
                m_shutdownCommandsFile = startupConfig.GetString("shutdown_console_commands_file", String.Empty);

                m_timedScript = startupConfig.GetString("timer_Script", "disabled");
            }

            base.ReadConfigSettings();
        }

        /// <summary>
        /// Performs initialisation of the scene, such as loading configuration from disk.
        /// </summary>
        public override void Startup()
        {
            m_log.Info("====================================================================");
            m_log.Info("========================= STARTING OPENSIM =========================");
            m_log.Info("====================================================================");
            m_log.InfoFormat("[OPENSIM MAIN]: Running in {0} mode", (m_sandbox ? "sandbox" : "grid"));

            m_console = new ConsoleBase("Region", this);
            MainConsole.Instance = m_console;

            base.Startup();

            //Run Startup Commands
            if (String.IsNullOrEmpty( m_startupCommandsFile ))
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
                m_scriptTimer.Interval = 1200 * 1000;
                m_scriptTimer.Elapsed += RunAutoTimerScript;
            }

            PrintFileToConsole("startuplogo.txt");
            RegisterCmd("echoTest", RunEchoTest, "this echos your command args to see how they are parsed");
            RegisterCmd("kickuser", KickUserCommand, "kickuser [first] [last] - attempts to log off a user from any region we are serving");

            // For now, start at the 'root' level by default
            ChangeSelectedRegion(new string[] {"root"});
        }

        private void RunAutoTimerScript(object sender, EventArgs e)
        {
            if (m_timedScript != "disabled")
            {
                RunCommandScript(m_timedScript);
            }
        }

        #region Console Commands

        private void RunEchoTest(string[] cmdparams)
        {
            for (int i = 0; i < cmdparams.Length; i++)
            {
                m_log.Info("[EchoTest]:  <arg" + i + ">"+cmdparams[i]+"</arg" + i + ">");
            }
        }

        private void KickUserCommand(string[] cmdparams)
        {
            if (cmdparams.Length < 2)
                return;

            IList agents = m_sceneManager.GetCurrentSceneAvatars();

            foreach (ScenePresence presence in agents)
            {
                RegionInfo regionInfo = m_sceneManager.GetRegionInfo(presence.RegionHandle);

                if (presence.Firstname.ToLower().Contains(cmdparams[0].ToLower()) && presence.Lastname.ToLower().Contains(cmdparams[1].ToLower()))
                {
                    m_console.Notice(
                        String.Format(
                             "Kicking user: {0,-16}{1,-16}{2,-37} in region: {3,-16}",
                             presence.Firstname,
                             presence.Lastname,
                             presence.UUID,
                             regionInfo.RegionName));

                    presence.Scene.CloseConnection(regionInfo.RegionHandle, presence.UUID);
                }
            }
            m_console.Notice("");
        }

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
                string currentCommand;
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
        /// Runs commands issued by the server console from the operator
        /// </summary>
        /// <param name="command">The first argument of the parameter (the command)</param>
        /// <param name="cmdparams">Additional arguments passed to the command</param>
        public override void RunCmd(string command, string[] cmdparams)
        {
            base.RunCmd(command, cmdparams);
            RunPluginCommands(command , cmdparams);

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
                    Debug(cmdparams);
                    break;

                case "save-xml":
                    SaveXml(cmdparams);
                    break;

                case "load-xml":
                    LoadXml(cmdparams);
                    break;

                case "save-xml2":
                    SaveXml2(cmdparams);
                    break;

                case "load-xml2":
                    LoadXml2(cmdparams);
                    break;

                case "save-prims-xml2":
                    if (cmdparams.Length > 1)
                    {
                        m_sceneManager.SaveNamedPrimsToXml2(cmdparams[0], cmdparams[1]);
                    }
                    else
                    {
                        m_sceneManager.SaveNamedPrimsToXml2("Primitive", DEFAULT_PRIM_BACKUP_FILENAME);
                    }
                    break;

                case "load-oar":
                    LoadOar(cmdparams);
                    break;

                case "save-oar":
                    SaveOar(cmdparams);
                    break;

                case "save-inv":
                    SaveInv(cmdparams);
                    break;

                case "plugin":
                    m_sceneManager.SendCommandToPluginModules(cmdparams);
                    break;

                case "command-script":
                    if (cmdparams.Length > 0)
                    {
                        RunCommandScript(cmdparams[0]);
                    }
                    break;

                case "backup":
                    m_sceneManager.BackupCurrentScene();
                    break;

                case "alert":
                    m_sceneManager.HandleAlertCommandOnCurrentScene(cmdparams);
                    break;

                case "create":
                    Create(cmdparams);
                    break;

                case "create-region":
                    string regionsDir = ConfigSource.Source.Configs["Startup"].GetString("regionload_regionsdir", "Regions").Trim();
                    CreateRegion(new RegionInfo(cmdparams[0], String.Format("{0}/{1}", regionsDir, cmdparams[1]), false), true);
                    break;

                case "remove-region":
                    string regRemoveName = CombineParams(cmdparams, 0);

                    Scene removeScene;
                    if (m_sceneManager.TryGetScene(regRemoveName, out removeScene))
                        RemoveRegion(removeScene, false);
                    else
                        m_console.Error("no region with that name");
                    break;

                case "delete-region":
                    string regDeleteName = CombineParams(cmdparams, 0);

                    Scene killScene;
                    if (m_sceneManager.TryGetScene(regDeleteName, out killScene))
                        RemoveRegion(killScene, true);
                    else
                        m_console.Error("no region with that name");
                    break;

                case "restart":
                    m_sceneManager.RestartCurrentScene();
                    break;

                case "change-region":
                    ChangeSelectedRegion(cmdparams);
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
                                    m_config.Source.Merge(c.ConfigSource);

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
                                    foreach (IRegionModule rm in new ArrayList(m_moduleLoader.GetLoadedSharedModules))
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
                                    foreach (Scene s in new ArrayList(m_sceneManager.Scenes))
                                    {

                                        m_console.Notice("Loading module: " + cmdparams[1]);
                                        m_moduleLoader.LoadRegionModules(cmdparams[1], s);
                                    }
                                }
                                break;
                        }
                    }

                    break;

                case "Add-InventoryHost":
                    if (cmdparams.Length > 0)
                    {
                        m_commsManager.AddInventoryService(cmdparams[0]);
                    }
                    break;
                                    
                case "reset":
                    Reset(cmdparams);
                    break;                

                default:
                    string[] tmpPluginArgs = new string[cmdparams.Length + 1];
                    cmdparams.CopyTo(tmpPluginArgs, 1);
                    tmpPluginArgs[0] = command;

                    m_sceneManager.SendCommandToPluginModules(tmpPluginArgs);
                    break;
            }
        }

        /// <summary>
        /// Change the currently selected region.  The selected region is that operated upon by single region commands.
        /// </summary>
        /// <param name="cmdParams"></param>
        protected void ChangeSelectedRegion(string[] cmdparams)
        {
            if (cmdparams.Length > 0)
            {
                string newRegionName = CombineParams(cmdparams, 0);

                if (!m_sceneManager.TrySetCurrentScene(newRegionName))
                    m_console.Error("Couldn't select region " + newRegionName);
            }
            else
            {
                m_console.Error("Usage: change-region <region name>");
            }

            string regionName = (m_sceneManager.CurrentScene == null ? "root" : m_sceneManager.CurrentScene.RegionInfo.RegionName);
            m_console.Notice(String.Format("Currently selected region is {0}", regionName));
            m_console.DefaultPrompt = String.Format("Region ({0}) ", regionName);
        }

        /// <summary>
        /// Execute switch for some of the create commands
        /// </summary>
        /// <param name="args"></param>
        protected void Create(string[] args)
        {
            if (args.Length == 0)
                return;

            switch (args[0])
            {
                case "user":
                    CreateUser(args);
                    break;
            }
        }
        
        /// <summary>
        /// Execute switch for some of the reset commands
        /// </summary>
        /// <param name="args"></param>
        protected void Reset(string[] args)
        {
            if (args.Length == 0)
                return;

            switch (args[0])
            {
                case "user":
                
                    switch (args[1])
                    {
                        case "password":
                            ResetUserPassword(args);
                            break;
                    }
                
                    break;
            }
        }        

        /// <summary>
        /// Turn on some debugging values for OpenSim.
        /// </summary>
        /// <param name="args"></param>
        protected void Debug(string[] args)
        {
            if (args.Length == 0)
                return;

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

                case "scene":
                    if (args.Length == 4)
                    {
                        if (m_sceneManager.CurrentScene == null)
                        {
                            m_console.Error("CONSOLE", "Please use 'change-region <regioname>' first");
                        }
                        else
                        {
                            bool scriptingOn = !Convert.ToBoolean(args[1]);
                            bool collisionsOn = !Convert.ToBoolean(args[2]);
                            bool physicsOn = !Convert.ToBoolean(args[3]);
                            m_sceneManager.CurrentScene.SetSceneCoreDebug(scriptingOn, collisionsOn, physicsOn);

                            m_console.Notice(
                                "CONSOLE",
                                String.Format(
                                    "Set debug scene scripting = {0}, collisions = {1}, physics = {2}",
                                    !scriptingOn, !collisionsOn, !physicsOn));
                        }
                    }
                    else
                    {
                        m_console.Error("debug scene <scripting> <collisions> <physics> (where inside <> is true/false)");
                    }

                    break;

                default:
                    m_console.Error("Unknown debug");
                    break;
            }
        }

        protected override void ShowHelp(string[] helpArgs)
        {
            base.ShowHelp(helpArgs);

            m_console.Notice("alert - send alert to a designated user or all users.");
            m_console.Notice("  alert [First] [Last] [Message] - send an alert to a user. Case sensitive.");
            m_console.Notice("  alert general [Message] - send an alert to all users.");
            m_console.Notice("backup - persist simulator objects to the database ahead of the normal schedule.");
            m_console.Notice("clear-assets - clear the asset cache");
            m_console.Notice("create-region <name> <regionfile.xml> - create a new region");
            m_console.Notice("change-region <name> - select the region that single region commands operate upon.");
            m_console.Notice("command-script [filename] - Execute command in a file.");
            m_console.Notice("debug - debugging commands");
            m_console.Notice("  debug packet 0..255 - print incoming/outgoing packets (0=off)");
            m_console.Notice("  debug scene [scripting] [collision] [physics] - Enable/Disable debug stuff, each can be True/False");
            m_console.Notice("edit-scale [prim name] [x] [y] [z] - resize given prim");
            m_console.Notice("export-map [filename] - save image of world map");
            m_console.Notice("force-update - force an update of prims in the scene");
            m_console.Notice("restart - disconnects all clients and restarts the sims in the instance.");
            m_console.Notice("remove-region [name] - remove a region");
            m_console.Notice("delete-region [name] - delete a region and its associated region file");
            m_console.Notice("load-xml [filename] - load prims from XML (DEPRECATED)");
            m_console.Notice("save-xml [filename] - save prims to XML (DEPRECATED)");
            m_console.Notice("save-xml2 [filename] - save prims to XML using version 2 format");
            m_console.Notice("load-xml2 [filename] - load prims from XML using version 2 format");
            m_console.Notice("load-oar [filename] - load an OpenSimulator region archive.  This replaces everything in the current region.");
            m_console.Notice("save-oar [filename] - Save the current region to an OpenSimulator region archive.");
            m_console.Notice("script - manually trigger scripts? or script commands?");
            m_console.Notice("set-time [x] - set the current scene time phase");
            m_console.Notice("show assets - show state of asset cache.");
            m_console.Notice("show users - show info about connected users (only root agents).");
            m_console.Notice("show users full - show info about connected users (root and child agents).");
            m_console.Notice("show modules - shows info about loaded modules.");
            m_console.Notice("show regions - show running region information.");
            m_console.Notice("config set section field value - set a config value");
            m_console.Notice("config get section field - get a config value");
            m_console.Notice("config save - save OpenSim.ini");
            m_console.Notice("terrain help - show help for terrain commands.");

            ShowPluginCommandsHelp(CombineParams(helpArgs, 0), m_console);

            if (m_sandbox)
            {
                m_console.Notice("");
                m_console.Notice("create user - adds a new user.");
                m_console.Notice("reset user password - reset a user's password.");
            }
        }

        // see BaseOpenSimServer
        public override void Show(string[] showParams)
        {
            base.Show(showParams);

            switch (showParams[0])
            {
                case "assets":
                    m_assetCache.ShowState();
                    break;

                case "users":
                    IList agents;
                    if (showParams.Length > 1 && showParams[1] == "full")
                    {
                        agents = m_sceneManager.GetCurrentScenePresences();
                    }
                    else
                    {
                        agents = m_sceneManager.GetCurrentSceneAvatars();
                    }

                    m_console.Notice(String.Format("\nAgents connected: {0}\n", agents.Count));

                    m_console.Notice(
                        String.Format("{0,-16}{1,-16}{2,-37}{3,-11}{4,-16}", "Firstname", "Lastname",
                                      "Agent ID", "Root/Child", "Region"));

                    foreach (ScenePresence presence in agents)
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

                        m_console.Notice(
                            String.Format(
                                "{0,-16}{1,-16}{2,-37}{3,-11}{4,-16}",
                                presence.Firstname,
                                presence.Lastname,
                                presence.UUID,
                                presence.IsChildAgent ? "Child" : "Root",
                                regionName));
                    }

                    m_console.Notice("");
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
                                             scene.RegionInfo.RegionLocY + " , Region Port: " + scene.RegionInfo.InternalEndPoint.Port.ToString());
                        });
                    break;
            }
        }

        /// <summary>
        /// Create a new user
        /// </summary>
        /// <param name="cmdparams"></param>
        protected void CreateUser(string[] cmdparams)
        {
            string firstName;
            string lastName;
            string password;
            uint regX = 1000;
            uint regY = 1000;

            if (cmdparams.Length < 2)
                firstName = MainConsole.Instance.CmdPrompt("First name", "Default");
            else firstName = cmdparams[1];

            if ( cmdparams.Length < 3 )
                lastName = MainConsole.Instance.CmdPrompt("Last name", "User");
            else lastName = cmdparams[2];

            if ( cmdparams.Length < 4 )
                password = MainConsole.Instance.PasswdPrompt("Password");
            else password = cmdparams[3];

            if ( cmdparams.Length < 5 )
                regX = Convert.ToUInt32(MainConsole.Instance.CmdPrompt("Start Region X", regX.ToString()));
            else regX = Convert.ToUInt32(cmdparams[4]);

            if ( cmdparams.Length < 6 )
                regY = Convert.ToUInt32(MainConsole.Instance.CmdPrompt("Start Region Y", regY.ToString()));
            else regY = Convert.ToUInt32(cmdparams[5]);

            if (null == m_commsManager.UserService.GetUserProfile(firstName, lastName))
            {
                CreateUser(firstName, lastName, password, regX, regY);
            }
            else
            {
                m_log.ErrorFormat("[CONSOLE]: A user with the name {0} {1} already exists!", firstName, lastName);
            }
        }
        
        /// <summary>
        /// Reset a user password.
        /// </summary>
        /// <param name="cmdparams"></param>
        private void ResetUserPassword(string[] cmdparams)
        {
            string firstName;
            string lastName;
            string newPassword;
            
            if (cmdparams.Length < 3)
                firstName = MainConsole.Instance.CmdPrompt("First name");
            else firstName = cmdparams[2];

            if ( cmdparams.Length < 4 )
                lastName = MainConsole.Instance.CmdPrompt("Last name");
            else lastName = cmdparams[3];

            if ( cmdparams.Length < 5 )
                newPassword = MainConsole.Instance.PasswdPrompt("New password");
            else newPassword = cmdparams[4];
            
            m_commsManager.ResetUserPassword(firstName, lastName, newPassword);
        }                        

        protected void SaveXml(string[] cmdparams)
        {
            m_log.Error("[CONSOLE]: PLEASE NOTE, save-xml is DEPRECATED and may be REMOVED soon.  If you are using this and there is some reason you can't use save-xml2, please file a mantis detailing the reason.");

            if (cmdparams.Length > 0)
            {
                m_sceneManager.SaveCurrentSceneToXml(cmdparams[0]);
            }
            else
            {
                m_sceneManager.SaveCurrentSceneToXml(DEFAULT_PRIM_BACKUP_FILENAME);
            }
        }

        protected void LoadXml(string[] cmdparams)
        {
            m_log.Error("[CONSOLE]: PLEASE NOTE, load-xml is DEPRECATED and may be REMOVED soon.  If you are using this and there is some reason you can't use load-xml2, please file a mantis detailing the reason.");

            Vector3 loadOffset = new Vector3(0, 0, 0);
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
        }

        protected void SaveXml2(string[] cmdparams)
        {
            if (cmdparams.Length > 0)
            {
                m_sceneManager.SaveCurrentSceneToXml2(cmdparams[0]);
            }
            else
            {
                m_sceneManager.SaveCurrentSceneToXml2(DEFAULT_PRIM_BACKUP_FILENAME);
            }
        }

        protected void LoadXml2(string[] cmdparams)
        {
            if (cmdparams.Length > 0)
            {
                m_sceneManager.LoadCurrentSceneFromXml2(cmdparams[0]);
            }
            else
            {
                m_sceneManager.LoadCurrentSceneFromXml2(DEFAULT_PRIM_BACKUP_FILENAME);
            }
        }

        /// <summary>
        /// Load a whole region from an opensim archive.
        /// </summary>
        /// <param name="cmdparams"></param>
        protected void LoadOar(string[] cmdparams)
        {
            m_log.Warn("[CONSOLE]: Experimental functionality.  Please don't rely on this yet.");
            m_log.Warn("[CONSOLE]: See http://opensimulator.org/wiki/OpenSim_Archives for more details.");

            if (cmdparams.Length > 0)
            {
                m_sceneManager.LoadArchiveToCurrentScene(cmdparams[0]);
            }
            else
            {
                m_sceneManager.LoadArchiveToCurrentScene(DEFAULT_OAR_BACKUP_FILENAME);
            }
        }

        /// <summary>
        /// Save a region to a file, including all the assets needed to restore it.
        /// </summary>
        /// <param name="cmdparams"></param>
        protected void SaveOar(string[] cmdparams)
        {
            m_log.Warn("[CONSOLE]: Experimental functionality.  Please don't rely on this yet.");
            m_log.Warn("[CONSOLE]: See http://opensimulator.org/wiki/OpenSim_Archives for more details.");

            if (cmdparams.Length > 0)
            {
                m_sceneManager.SaveCurrentSceneToArchive(cmdparams[0]);
            }
            else
            {
                m_sceneManager.SaveCurrentSceneToArchive(DEFAULT_OAR_BACKUP_FILENAME);
            }
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

        /// <summary>
        /// Runs the best matching plugin command
        ///
        /// returns true if a match was found, false otherwise.
        /// </summary>
        public bool RunPluginCommands(string cmd, string[] withParams)
        {
            ConsolePluginCommand bestMatch = null;
            int bestLength = 0;
            String cmdWithParams = cmd + " " + String.Join(" ",withParams);
            foreach (ConsolePluginCommand cmdinfo in m_PluginCommandInfos)
            {
                int matchLen = cmdinfo.matchLength(cmdWithParams);
                if (matchLen > bestLength)
                {
                    bestMatch = cmdinfo;
                    bestLength = matchLen;
                }
            }
            if (bestMatch == null) return false;
            bestMatch.Run(cmd,withParams);//.Substring(bestLength));
            return true;
        }

        /// <summary>
        /// Show the matching plugins command help
        /// </summary>
        public void ShowPluginCommandsHelp(string cmdWithParams, ConsoleBase console)
        {
            foreach (ConsolePluginCommand cmdinfo in m_PluginCommandInfos)
            {
                if (cmdinfo.IsHelpfull(cmdWithParams))
                {
                    cmdinfo.ShowHelp(console);
                }
            }
        }

        /// <summary>
        /// Registers a new console plugin command
        /// </summary>
        public static void RegisterCmd(string cmd, ConsoleCommand deligate, string help)
        {
            RegisterConsolePluginCommand(new ConsolePluginCommand(cmd, deligate, help));
        }

        /// <summary>
        /// Registers a new console plugin command
        /// </summary>
        public static void RegisterConsolePluginCommand(ConsolePluginCommand pluginCommand)
        {
            m_PluginCommandInfos.Add(pluginCommand);
        }
        #endregion
    }
}
