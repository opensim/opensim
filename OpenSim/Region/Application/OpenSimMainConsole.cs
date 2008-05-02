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
using libsecondlife;
using log4net;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Framework.Statistics;
using OpenSim.Region.ClientStack.LindenUDP;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;
using Timer=System.Timers.Timer;

namespace OpenSim
{
    public delegate void ConsoleCommand(string[] comParams);

    public class OpenSimMainConsole : OpenSimMain, conscmd_callback
    {        
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected string m_startupCommandsFile;
        protected string m_shutdownCommandsFile;

        private string m_timedScript = "disabled";
        private Timer m_scriptTimer;


        public OpenSimMainConsole(IConfigSource configSource)
            : base(configSource)
        {
        }

        protected override void ReadConfigSettings()
        {
            IConfig startupConfig = m_config.Configs["Startup"];

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
            InternalStartUp();

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
                m_scriptTimer.Interval = 1200 * 1000;
                m_scriptTimer.Elapsed += RunAutoTimerScript;
            }
            PrintFileToConsole("startuplogo.txt");
        }

        protected ConsoleBase CreateConsole()
        {
            return new ConsoleBase("Region", this);
        }

        /// <summary>
        /// Performs any last-minute sanity checking and shuts down the region server
        /// </summary>
        public override void Shutdown()
        {
            if (m_startupCommandsFile != String.Empty)
            {
                RunCommandScript(m_shutdownCommandsFile);
            }
            InternalShutdown();
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
                    if (cmdparams.Length == 3)
                    {
                        if (m_sceneManager.CurrentScene == null)
                        {
                            m_console.Error("CONSOLE", "Please use 'change-region <regioname>' first");
                        }
                        else
                        {
                            m_sceneManager.CurrentScene.SetSceneCoreDebug(!Convert.ToBoolean(cmdparams[0]), !Convert.ToBoolean(cmdparams[1]), !Convert.ToBoolean(cmdparams[2]));
                        }
                    }
                    else
                    {
                        m_console.Error("scene-debug <scripting> <collisions> <physics> (where inside <> is true/false)");
                    }
                    break;

                case "help":
                    m_console.Notice("alert - send alert to a designated user or all users.");
                    m_console.Notice("  alert [First] [Last] [Message] - send an alert to a user. Case sensitive.");
                    m_console.Notice("  alert general [Message] - send an alert to all users.");
                    m_console.Notice("backup - trigger a simulator backup");
                    m_console.Notice("clear-assets - clear asset cache");
                    m_console.Notice("create-region <name> <regionfile.xml> - creates a new region");
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
//                     m_console.Notice("THREAD", Process.GetCurrentProcess().Threads.Count + " threads running:");
//                     int _tc = 0;
                    
//                     foreach (ProcessThread pt in Process.GetCurrentProcess().Threads)
//                     {
//                         _tc++;
//                         m_console.Notice("THREAD", _tc + ": ID: " + pt.Id + ", Started: " + pt.StartTime.ToString() + ", CPU time: " + pt.TotalProcessorTime + ", Pri: " + pt.BasePriority.ToString() + ", State: " + pt.ThreadState.ToString());
//                     }

                    List<Thread> threads = ThreadTracker.GetThreads();
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

                case "plugin":
                    m_sceneManager.SendCommandToPluginModules(cmdparams);
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
                        // only need to check this if we are not at the
                        // root level 
                        if ((m_sceneManager.CurrentScene != null) &&
                            (m_sceneManager.CurrentScene.RegionInfo.RegionID == killScene.RegionInfo.RegionID))
                        {
                            m_sceneManager.TrySetCurrentScene("..");
                        }
                        m_regionData.Remove(killScene.RegionInfo);
                        m_sceneManager.CloseScene(killScene);
                    }
                    break;

                case "exit":
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

                default:
                    string[] tmpPluginArgs = new string[cmdparams.Length + 1];
                    cmdparams.CopyTo(tmpPluginArgs, 1);
                    tmpPluginArgs[0] = command;

                    m_sceneManager.SendCommandToPluginModules(tmpPluginArgs);
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
