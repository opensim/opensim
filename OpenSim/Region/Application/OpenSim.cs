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
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime;
using System.Text;
using System.Text.RegularExpressions;
using System.Timers;
using System.Net;
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
using OpenSim.Services.Interfaces;
using OpenSim.Framework.Servers.HttpServer;

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
        private System.Timers.Timer m_scriptTimer;

        public OpenSim(IConfigSource configSource) : base(configSource)
        {
        }

        protected override void ReadExtraConfigSettings()
        {
            base.ReadExtraConfigSettings();

            IConfig startupConfig = Config.Configs["Startup"];
            IConfig networkConfig = Config.Configs["Network"];

            int stpMinThreads = 2;
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

                string asyncCallMethodStr = startupConfig.GetString("async_call_method", String.Empty);
                FireAndForgetMethod asyncCallMethod;
                if (!String.IsNullOrEmpty(asyncCallMethodStr) && Utils.EnumTryParse<FireAndForgetMethod>(asyncCallMethodStr, out asyncCallMethod))
                    Util.FireAndForgetMethod = asyncCallMethod;

                stpMinThreads = startupConfig.GetInt("MinPoolThreads", 2 );
                stpMaxThreads = startupConfig.GetInt("MaxPoolThreads", 25);
                m_consolePrompt = startupConfig.GetString("ConsolePrompt", @"Region (\R) ");

                int dnsTimeout = startupConfig.GetInt("DnsTimeout", 30000);
                try { ServicePointManager.DnsRefreshTimeout = dnsTimeout; } catch { }
            }

            if (Util.FireAndForgetMethod == FireAndForgetMethod.SmartThreadPool)
                Util.InitThreadPool(stpMinThreads, stpMaxThreads);

            m_log.Info("[OPENSIM MAIN]: Using async_call_method " + Util.FireAndForgetMethod);

            m_log.InfoFormat("[OPENSIM MAIN] Running GC in {0} mode", GCSettings.IsServerGC ? "server":"workstation");
        }

#if (_MONO)
        private static Mono.Unix.UnixSignal[] signals;


        private Thread signal_thread = new Thread (delegate ()
        {
            while (true)
            {
                // Wait for a signal to be delivered
                int index = Mono.Unix.UnixSignal.WaitAny (signals, -1);

                //Mono.Unix.Native.Signum signal = signals [index].Signum;
                MainConsole.Instance.RunCommand("shutdown");
            }
        });       
#endif

        /// <summary>
        /// Performs initialisation of the scene, such as loading configuration from disk.
        /// </summary>
        protected override void StartupSpecific()
        {
            m_log.Info("====================================================================");
            m_log.Info("========================= STARTING OPENSIM =========================");
            m_log.Info("====================================================================");

#if (_MONO)
            if(!Util.IsWindows())
            {
                try
                {
                    // linux mac os specifics
                    signals = new Mono.Unix.UnixSignal[]
                    {
                        new Mono.Unix.UnixSignal(Mono.Unix.Native.Signum.SIGTERM)
                    };
                    signal_thread.IsBackground = true;
                    signal_thread.Start();
                }
                catch (Exception e)
                {
                    m_log.Info("Could not set up UNIX signal handlers. SIGTERM will not");
                    m_log.InfoFormat("shut down gracefully: {0}", e.Message);
                    m_log.Debug("Exception was: ", e);
                }
            }
#endif
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
                    break;
                default:
                    m_console = new LocalConsole("Region", Config.Configs["Startup"]);
                    break;
                }
            }

            m_console.ReadConfig(Config);

            MainConsole.Instance = m_console;

            LogEnvironmentInformation();
            RegisterCommonAppenders(Config.Configs["Startup"]);
            RegisterConsoleCommands();

            base.StartupSpecific();

            MainServer.Instance.AddStreamHandler(new OpenSim.SimStatusHandler());
            MainServer.Instance.AddStreamHandler(new OpenSim.XSimStatusHandler(this));
            if (userStatsURI != String.Empty)
                MainServer.Instance.AddStreamHandler(new OpenSim.UXSimStatusHandler(this));
            MainServer.Instance.AddStreamHandler(new OpenSim.SimRobotsHandler());

            if (managedStatsURI != String.Empty)
            {
                string urlBase = String.Format("/{0}/", managedStatsURI);
                StatsManager.StatsPassword = managedStatsPassword;
                MainServer.Instance.AddHTTPHandler(urlBase, StatsManager.HandleStatsRequest);
                m_log.InfoFormat("[OPENSIM] Enabling remote managed stats fetch. URL = {0}", urlBase);
            }

            MethodInfo mi = m_console.GetType().GetMethod("SetServer", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(BaseHttpServer) }, null);

            if (mi != null)
            {
                if (m_consolePort == 0)
                    mi.Invoke(m_console, new object[] { m_httpServer });
                else
                    mi.Invoke(m_console, new object[] { MainServer.GetHttpServer(m_consolePort) });
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
                m_scriptTimer = new System.Timers.Timer();
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

            m_console.Commands.AddCommand("General", false, "change region",
                                          "change region <region name>",
                                          "Change current console region",
                                          ChangeSelectedRegion);

            m_console.Commands.AddCommand("Archiving", false, "save xml",
                                          "save xml [<file name>]",
                                          "Save a region's data in XML format",
                                          SaveXml);

            m_console.Commands.AddCommand("Archiving", false, "save xml2",
                                          "save xml2 [<file name>]",
                                          "Save a region's data in XML2 format",
                                          SaveXml2);

            m_console.Commands.AddCommand("Archiving", false, "load xml",
                                          "load xml [<file name> [-newUID [<x> <y> <z>]]]",
                                          "Load a region's data from XML format",
                                          LoadXml);

            m_console.Commands.AddCommand("Archiving", false, "load xml2",
                                          "load xml2 [<file name>]",
                                          "Load a region's data from XML2 format",
                                          LoadXml2);

            m_console.Commands.AddCommand("Archiving", false, "save prims xml2",
                                          "save prims xml2 [<prim name> <file name>]",
                                          "Save named prim to XML2",
                                          SavePrimsXml2);

            m_console.Commands.AddCommand("Archiving", false, "load oar",
                                          "load oar [-m|--merge] [-s|--skip-assets]"
                                             + " [--default-user \"User Name\"]"
                                             + " [--force-terrain] [--force-parcels]"
                                             + " [--no-objects]"
                                             + " [--rotation degrees]"
                                             + " [--bounding-origin \"<x,y,z>\"]"
                                             + " [--bounding-size \"<x,y,z>\"]"
                                             + " [--displacement \"<x,y,z>\"]"
                                             + " [-d|--debug]"
                                             + " [<OAR path>]",
                                          "Load a region's data from an OAR archive.",
                                          "--merge will merge the OAR with the existing scene (suppresses terrain and parcel info loading).\n"
                                            + "--skip-assets will load the OAR but ignore the assets it contains.\n"
                                            + "--default-user will use this user for any objects with an owner whose UUID is not found in the grid.\n"
                                            + "--force-terrain forces the loading of terrain from the oar (undoes suppression done by --merge).\n"
                                            + "--force-parcels forces the loading of parcels from the oar (undoes suppression done by --merge).\n"
                                            + "--no-objects suppresses the addition of any objects (good for loading only the terrain).\n"
                                            + "--rotation specified rotation to be applied to the oar. Specified in degrees.\n"
                                            + "--bounding-origin will only place objects that after displacement and rotation fall within the bounding cube who's position starts at <x,y,z>. Defaults to <0,0,0>.\n"
                                            + "--bounding-size specifies the size of the bounding cube. The default is the size of the destination region and cannot be larger than this.\n"
                                            + "--displacement will add this value to the position of every object loaded.\n"
                                            + "--debug forces the archiver to display messages about where each object is being placed.\n\n"
                                            + "The path can be either a filesystem location or a URI.\n"
                                            + "  If this is not given then the command looks for an OAR named region.oar in the current directory."
                                            + "  [--rotation-center \"<x,y,z>\"] used to be an option, now it does nothing and will be removed soon."
                                            + "When an OAR is being loaded, operations are applied in this order:\n"
                                            + "1: Rotation (around the incoming OARs region center)\n"
                                            + "2: Cropping (a bounding cube with origin and size)\n"
                                            + "3: Displacement (setting offset coordinates within the destination region)",
                                          LoadOar); ;

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
                                          "Change the scale of a named prim",
                                          HandleEditScale);

            m_console.Commands.AddCommand("Objects", false, "rotate scene",
                                          "rotate scene <degrees> [centerX, centerY]",
                                          "Rotates all scene objects around centerX, centerY (default 128, 128) (please back up your region before using)",
                                          HandleRotateScene);

            m_console.Commands.AddCommand("Objects", false, "scale scene",
                                          "scale scene <factor>",
                                          "Scales the scene objects (please back up your region before using)",
                                          HandleScaleScene);

            m_console.Commands.AddCommand("Objects", false, "translate scene",
                                          "translate scene xOffset yOffset zOffset",
                                          "translates the scene objects (please back up your region before using)",
                                          HandleTranslateScene);

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
                                          "Show connection data",
                                          HandleShow);

            m_console.Commands.AddCommand("Comms", false, "show circuits",
                                          "show circuits",
                                          "Show agent circuit data",
                                          HandleShow);

            m_console.Commands.AddCommand("Comms", false, "show pending-objects",
                                          "show pending-objects",
                                          "Show # of objects on the pending queues of all scene viewers",
                                          HandleShow);

            m_console.Commands.AddCommand("General", false, "show modules",
                                          "show modules",
                                          "Show module data",
                                          HandleShow);

            m_console.Commands.AddCommand("Regions", false, "show regions",
                                          "show regions",
                                          "Show region data",
                                          HandleShow);

            m_console.Commands.AddCommand("Regions", false, "show ratings",
                                          "show ratings",
                                          "Show rating data",
                                          HandleShow);

            m_console.Commands.AddCommand("Objects", false, "backup",
                                          "backup",
                                          "Persist currently unsaved object changes immediately instead of waiting for the normal persistence call.",
                                          RunCommand);

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
                                          "Restart the currently selected region(s) in this instance",
                                          RunCommand);

            m_console.Commands.AddCommand("General", false, "command-script",
                                          "command-script <script>",
                                          "Run a command script from file",
                                          RunCommand);

            m_console.Commands.AddCommand("Regions", false, "remove-region",
                                          "remove-region <name>",
                                          "Remove a region from this simulator",
                                          RunCommand);

            m_console.Commands.AddCommand("Regions", false, "delete-region",
                                          "delete-region <name>",
                                          "Delete a region from disk",
                                          RunCommand);

            m_console.Commands.AddCommand("Estates", false, "estate create",
                                          "estate create <owner UUID> <estate name>",
                                          "Creates a new estate with the specified name, owned by the specified user."
                                          + " Estate name must be unique.",
                                          CreateEstateCommand);

            m_console.Commands.AddCommand("Estates", false, "estate set owner",
                                          "estate set owner <estate-id>[ <UUID> | <Firstname> <Lastname> ]",
                                          "Sets the owner of the specified estate to the specified UUID or user. ",
                                          SetEstateOwnerCommand);

            m_console.Commands.AddCommand("Estates", false, "estate set name",
                                          "estate set name <estate-id> <new name>",
                                          "Sets the name of the specified estate to the specified value. New name must be unique.",
                                          SetEstateNameCommand);

            m_console.Commands.AddCommand("Estates", false, "estate link region",
                                          "estate link region <estate ID> <region ID>",
                                          "Attaches the specified region to the specified estate.",
                                          EstateLinkRegionCommand);
        }

        protected override void ShutdownSpecific()
        {
            if (m_shutdownCommandsFile != String.Empty)
            {
                RunCommandScript(m_shutdownCommandsFile);
            }

            if (m_timedScript != "disabled")
            {
                m_scriptTimer.Dispose();
                m_timedScript = "disabled";
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

                if (presence.Firstname.ToLower().Equals(mainParams[2].ToLower()) &&
                    presence.Lastname.ToLower().Equals(mainParams[3].ToLower()))
                {
                    MainConsole.Instance.Output(
                        String.Format(
                            "Kicking user: {0,-16} {1,-16} {2,-37} in region: {3,-16}",
                            presence.Firstname, presence.Lastname, presence.UUID, regionInfo.RegionName));

                    // kick client...
                    if (alert != null)
                        presence.ControllingClient.Kick(alert);
                    else
                        presence.ControllingClient.Kick("\nYou have been logged out by an administrator.\n");

                    presence.Scene.CloseAgent(presence.UUID, force);
                    break;
                }
            }

            MainConsole.Instance.Output("");
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

        private void HandleRotateScene(string module, string[] args)
        {
            string usage = "Usage: rotate scene <angle in degrees> [centerX centerY] (centerX and centerY are optional and default to Constants.RegionSize / 2";

            float centerX = Constants.RegionSize * 0.5f;
            float centerY = Constants.RegionSize * 0.5f;

            if (args.Length < 3 || args.Length == 4)
            {
                MainConsole.Instance.Output(usage);
                return;
            }

            float angle = (float)(Convert.ToSingle(args[2]) / 180.0 * Math.PI);
            OpenMetaverse.Quaternion rot = OpenMetaverse.Quaternion.CreateFromAxisAngle(0, 0, 1, angle);

            if (args.Length > 4)
            {
                centerX = Convert.ToSingle(args[3]);
                centerY = Convert.ToSingle(args[4]);
            }

            Vector3 center = new Vector3(centerX, centerY, 0.0f);

            SceneManager.ForEachSelectedScene(delegate(Scene scene)
            {
                scene.ForEachSOG(delegate(SceneObjectGroup sog)
                {
                    if (!sog.IsAttachment)
                    {
                        sog.RootPart.UpdateRotation(rot * sog.GroupRotation);
                        Vector3 offset = sog.AbsolutePosition - center;
                        offset *= rot;
                        sog.UpdateGroupPosition(center + offset);
                    }
                });
            });
        }

        private void HandleScaleScene(string module, string[] args)
        {
            string usage = "Usage: scale scene <factor>";

            if (args.Length < 3)
            {
                MainConsole.Instance.Output(usage);
                return;
            }

            float factor = (float)(Convert.ToSingle(args[2]));

            float minZ = float.MaxValue;

            SceneManager.ForEachSelectedScene(delegate(Scene scene)
            {
                scene.ForEachSOG(delegate(SceneObjectGroup sog)
                {
                    if (!sog.IsAttachment)
                    {
                        if (sog.RootPart.AbsolutePosition.Z < minZ)
                            minZ = sog.RootPart.AbsolutePosition.Z;
                    }
                });
            });

            SceneManager.ForEachSelectedScene(delegate(Scene scene)
            {
                scene.ForEachSOG(delegate(SceneObjectGroup sog)
                {
                    if (!sog.IsAttachment)
                    {
                        Vector3 tmpRootPos = sog.RootPart.AbsolutePosition;
                        tmpRootPos.Z -= minZ;
                        tmpRootPos *= factor;
                        tmpRootPos.Z += minZ;

                        foreach (SceneObjectPart sop in sog.Parts)
                        {
                            if (sop.ParentID != 0)
                                sop.OffsetPosition *= factor;
                            sop.Scale *= factor;
                        }

                        sog.UpdateGroupPosition(tmpRootPos);
                    }
                });
            });
        }

        private void HandleTranslateScene(string module, string[] args)
        {
            string usage = "Usage: translate scene <xOffset, yOffset, zOffset>";

            if (args.Length < 5)
            {
                MainConsole.Instance.Output(usage);
                return;
            }

            float xOFfset = (float)Convert.ToSingle(args[2]);
            float yOffset = (float)Convert.ToSingle(args[3]);
            float zOffset = (float)Convert.ToSingle(args[4]);

            Vector3 offset = new Vector3(xOFfset, yOffset, zOffset);

            SceneManager.ForEachSelectedScene(delegate(Scene scene)
            {
                scene.ForEachSOG(delegate(SceneObjectGroup sog)
                {
                    if (!sog.IsAttachment)
                        sog.UpdateGroupPosition(sog.AbsolutePosition + offset);
                });
            });
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
                MainConsole.Instance.Output(
                    "ERROR: Cannot create region {0} with ID {1}, this ID is already assigned to region {2}",
                    regInfo.RegionName, regInfo.RegionID, existingScene.RegionInfo.RegionName);

                return;
            }

            bool changed = PopulateRegionEstateInfo(regInfo);
            IScene scene;
            CreateRegion(regInfo, true, out scene);

            if (changed)
                m_estateDataService.StoreEstateSettings(regInfo.EstateSettings);

            scene.Start();
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
                else
                    RefreshPrompt();
            }
            else
            {
                MainConsole.Instance.Output("Usage: change region <region name>");
            }
        }

        /// <summary>
        /// Refreshs prompt with the current selection details.
        /// </summary>
        private void RefreshPrompt()
        {
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

        protected override void HandleRestartRegion(RegionInfo whichRegion)
        {
            base.HandleRestartRegion(whichRegion);

            // Where we are restarting multiple scenes at once, a previous call to RefreshPrompt may have set the
            // m_console.ConsoleScene to null (indicating all scenes).
            if (m_console.ConsoleScene != null && whichRegion.RegionName == ((Scene)m_console.ConsoleScene).Name)
                SceneManager.TrySetCurrentScene(whichRegion.RegionName);

            RefreshPrompt();
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
                    SceneManager.ForEachSelectedScene(
                        scene =>
                        {
                            MainConsole.Instance.Output("Loaded region modules in {0} are:", scene.Name);

                            List<IRegionModuleBase> sharedModules = new List<IRegionModuleBase>();
                            List<IRegionModuleBase> nonSharedModules = new List<IRegionModuleBase>();

                            foreach (IRegionModuleBase module in scene.RegionModules.Values)
                            {
                                if (module.GetType().GetInterface("ISharedRegionModule") == null)
                                    nonSharedModules.Add(module);
                                else
                                    sharedModules.Add(module);
                            }

                            foreach (IRegionModuleBase module in sharedModules.OrderBy(m => m.Name))
                                MainConsole.Instance.Output("New Region Module (Shared): {0}", module.Name);

                            foreach (IRegionModuleBase module in nonSharedModules.OrderBy(m => m.Name))
                                MainConsole.Instance.Output("New Region Module (Non-Shared): {0}", module.Name);
                        }
                    );

                    MainConsole.Instance.Output("");
                    break;

                case "regions":
                    ConsoleDisplayTable cdt = new ConsoleDisplayTable();
                    cdt.AddColumn("Name", ConsoleDisplayUtil.RegionNameSize);
                    cdt.AddColumn("ID", ConsoleDisplayUtil.UuidSize);
                    cdt.AddColumn("Position", ConsoleDisplayUtil.CoordTupleSize);
                    cdt.AddColumn("Size", 11);
                    cdt.AddColumn("Port", ConsoleDisplayUtil.PortSize);
                    cdt.AddColumn("Ready?", 6);
                    cdt.AddColumn("Estate", ConsoleDisplayUtil.EstateNameSize);
                    SceneManager.ForEachScene(
                        scene =>
                        {
                            RegionInfo ri = scene.RegionInfo;
                            cdt.AddRow(
                                ri.RegionName,
                                ri.RegionID,
                                string.Format("{0},{1}", ri.RegionLocX, ri.RegionLocY),
                                string.Format("{0}x{1}", ri.RegionSizeX, ri.RegionSizeY),
                                ri.InternalEndPoint.Port,
                                scene.Ready ? "Yes" : "No",
                                ri.EstateSettings.EstateName);
                        }
                    );

                    MainConsole.Instance.Output(cdt.ToString());
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
                            aCircuit.IPAddress != null ? aCircuit.IPAddress.ToString() : "not set",
                            Util.GetViewerName(aCircuit));
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
            cdt.AddColumn("ChildAgent?", 7);
            cdt.AddColumn("ping(ms)", 8);

            SceneManager.ForEachScene(
                s => s.ForEachClient(
                    c =>
                        {
                            bool child = false;
                            if(c.SceneAgent != null && c.SceneAgent.IsChildAgent)
                                child = true;
                            cdt.AddRow(
                            s.Name,
                            c.Name,
                            c.CircuitCode.ToString(),
                            c.RemoteEndPoint.ToString(),
                            c.IsActive.ToString(),
                            child.ToString(),
                            c.PingTimeMS);
                        }));

            MainConsole.Instance.Output(cdt.ToString());
        }

        /// <summary>
        /// Use XML2 format to serialize data to a file
        /// </summary>
        /// <param name="module"></param>
        /// <param name="cmdparams"></param>
        protected void SavePrimsXml2(string module, string[] cmdparams)
        {
            if (cmdparams.Length > 4)
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

            if (cmdparams.Length > 2)
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

        protected void CreateEstateCommand(string module, string[] args)
        {
            string response = null;
            UUID userID;

            if (args.Length == 2)
            {
                response = "No user specified.";
            }
            else if (!UUID.TryParse(args[2], out userID))
            {
                response = String.Format("{0} is not a valid UUID", args[2]);
            }
            else if (args.Length == 3)
            {
                response = "No estate name specified.";
            }
            else
            {
                Scene scene = SceneManager.CurrentOrFirstScene;

                // TODO: Is there a better choice here?
                UUID scopeID = UUID.Zero;
                UserAccount account = scene.UserAccountService.GetUserAccount(scopeID, userID);
                if (account == null)
                {
                    response = String.Format("Could not find user {0}", userID);
                }
                else
                {
                    // concatenate it all to "name"
                    StringBuilder sb = new StringBuilder(args[3]);
                    for (int i = 4; i < args.Length; i++)
                        sb.Append (" " + args[i]);
                    string estateName = sb.ToString().Trim();

                    // send it off for processing.
                    IEstateModule estateModule = scene.RequestModuleInterface<IEstateModule>();
                    response = estateModule.CreateEstate(estateName, userID);
                    if (response == String.Empty)
                    {
                        List<int> estates = scene.EstateDataService.GetEstates(estateName);
                        response = String.Format("Estate {0} created as \"{1}\"", estates.ElementAt(0), estateName);
                    }
                }
            }

            // give the user some feedback
            if (response != null)
                MainConsole.Instance.Output(response);
        }

        protected void SetEstateOwnerCommand(string module, string[] args)
        {
            string response = null;

            Scene scene = SceneManager.CurrentOrFirstScene;
            IEstateModule estateModule = scene.RequestModuleInterface<IEstateModule>();

            if (args.Length == 3)
            {
                response = "No estate specified.";
            }
            else
            {
                int estateId;
                if (!int.TryParse(args[3], out estateId))
                {
                    response = String.Format("\"{0}\" is not a valid ID for an Estate", args[3]);
                }
                else
                {
                    if (args.Length == 4)
                    {
                        response = "No user specified.";
                    }
                    else
                    {
                        UserAccount account = null;

                        // TODO: Is there a better choice here?
                        UUID scopeID = UUID.Zero;

                        string s1 = args[4];
                        if (args.Length == 5)
                        {
                            // attempt to get account by UUID
                            UUID u;
                            if (UUID.TryParse(s1, out u))
                            {
                                account = scene.UserAccountService.GetUserAccount(scopeID, u);
                                if (account == null)
                                    response = String.Format("Could not find user {0}", s1);
                            }
                            else
                            {
                                response = String.Format("Invalid UUID {0}", s1);
                            }
                        }
                        else
                        {
                            // attempt to get account by Firstname, Lastname
                            string s2 = args[5];
                            account = scene.UserAccountService.GetUserAccount(scopeID, s1, s2);
                            if (account == null)
                                response = String.Format("Could not find user {0} {1}", s1, s2);
                        }

                        // If it's valid, send it off for processing.
                        if (account != null)
                            response = estateModule.SetEstateOwner(estateId, account);

                        if (response == String.Empty)
                        {
                            response = String.Format("Estate owner changed to {0} ({1} {2})", account.PrincipalID, account.FirstName, account.LastName);
                        }
                    }
                }
            }

            // give the user some feedback
            if (response != null)
                MainConsole.Instance.Output(response);
        }

        protected void SetEstateNameCommand(string module, string[] args)
        {
            string response = null;

            Scene scene = SceneManager.CurrentOrFirstScene;
            IEstateModule estateModule = scene.RequestModuleInterface<IEstateModule>();

            if (args.Length == 3)
            {
                response = "No estate specified.";
            }
            else
            {
                int estateId;
                if (!int.TryParse(args[3], out estateId))
                {
                    response = String.Format("\"{0}\" is not a valid ID for an Estate", args[3]);
                }
                else
                {
                    if (args.Length == 4)
                    {
                        response = "No name specified.";
                    }
                    else
                    {
                        // everything after the estate ID is "name"
                        StringBuilder sb = new StringBuilder(args[4]);
                        for (int i = 5; i < args.Length; i++)
                            sb.Append (" " + args[i]);

                        string estateName = sb.ToString();

                        // send it off for processing.
                        response = estateModule.SetEstateName(estateId, estateName);

                        if (response == String.Empty)
                        {
                            response = String.Format("Estate {0} renamed to \"{1}\"", estateId, estateName);
                        }
                    }
                }
            }

            // give the user some feedback
            if (response != null)
                MainConsole.Instance.Output(response);
        }

        private void EstateLinkRegionCommand(string module, string[] args)
        {
            int estateId =-1;
            UUID regionId = UUID.Zero;
            Scene scene = null;
            string response = null;

            if (args.Length == 3)
            {
                response = "No estate specified.";
            }
            else if (!int.TryParse(args [3], out estateId))
            {
                response = String.Format("\"{0}\" is not a valid ID for an Estate", args [3]);
            }
            else if (args.Length == 4)
            {
                response = "No region specified.";
            }
            else if (!UUID.TryParse(args[4], out regionId))
            {
                response = String.Format("\"{0}\" is not a valid UUID for a Region", args [4]);
            }
            else if (!SceneManager.TryGetScene(regionId, out scene))
            {
                // region may exist, but on a different sim.
                response = String.Format("No access to Region \"{0}\"", args [4]);
            }

            if (response != null)
            {
                MainConsole.Instance.Output(response);
                return;
            }

            // send it off for processing.
            IEstateModule estateModule = scene.RequestModuleInterface<IEstateModule>();
            response = estateModule.SetRegionEstate(scene.RegionInfo, estateId);
            if (response == String.Empty)
            {
                estateModule.TriggerRegionInfoChange();
                estateModule.sendRegionHandshakeToAll();
                response = String.Format ("Region {0} is now attached to estate {1}", regionId, estateId);
            }

            // give the user some feedback
            if (response != null)
                MainConsole.Instance.Output (response);
        }

        #endregion

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
    }
}
