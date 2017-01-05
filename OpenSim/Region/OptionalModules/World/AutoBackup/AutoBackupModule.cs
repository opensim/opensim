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
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Timers;
using System.Text.RegularExpressions;
using log4net;
using Mono.Addins;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.OptionalModules.World.AutoBackup
{
    /// <summary>
    /// Choose between ways of naming the backup files that are generated.
    /// </summary>
    /// <remarks>Time: OARs are named by a timestamp.
    /// Sequential: OARs are named by counting (Region_1.oar, Region_2.oar, etc.)
    /// Overwrite: Only one file per region is created; it's overwritten each time a backup is made.</remarks>
    public enum NamingType
    {
        Time,
        Sequential,
        Overwrite
    }

    ///<summary>
    /// AutoBackupModule: save OAR region backups to disk periodically
    /// </summary>
    /// <remarks>
    /// Config Settings Documentation.
    /// Each configuration setting can be specified in two places: OpenSim.ini or Regions.ini.
    /// If specified in Regions.ini, the settings should be within the region's section name.
    /// If specified in OpenSim.ini, the settings should be within the [AutoBackupModule] section.
    /// Region-specific settings take precedence.
    ///
    /// AutoBackupModuleEnabled: True/False. Default: False. If True, use the auto backup module. This setting does not support per-region basis.
    ///     All other settings under [AutoBackupModule] are ignored if AutoBackupModuleEnabled is false, even per-region settings!
    /// AutoBackup: True/False. Default: False. If True, activate auto backup functionality.
    /// 	This is the only required option for enabling auto-backup; the other options have sane defaults.
    /// 	If False for a particular region, the auto-backup module becomes a no-op for the region, and all other AutoBackup* settings are ignored.
    /// 	If False globally (the default), only regions that specifically override it in Regions.ini will get AutoBackup functionality.
    /// AutoBackupInterval: Double, non-negative value. Default: 720 (12 hours).
    /// 	The number of minutes between each backup attempt.
    /// 	If a negative or zero value is given, it is equivalent to setting AutoBackup = False.
    /// AutoBackupBusyCheck: True/False. Default: True.
    /// 	If True, we will only take an auto-backup if a set of conditions are met.
    /// 	These conditions are heuristics to try and avoid taking a backup when the sim is busy.
    /// AutoBackupSkipAssets
    ///     If true, assets are not saved to the oar file. Considerably reduces impact on simulator when backing up. Intended for when assets db is backed up separately
    /// AutoBackupKeepFilesForDays
    ///     Backup files older than this value (in days) are deleted during the current backup process, 0 will disable this and keep all backup files indefinitely
    /// AutoBackupScript: String. Default: not specified (disabled).
    /// 	File path to an executable script or binary to run when an automatic backup is taken.
    ///  The file should really be (Windows) an .exe or .bat, or (Linux/Mac) a shell script or binary.
    ///  Trying to "run" directories, or things with weird file associations on Win32, might cause unexpected results!
    /// 	argv[1] of the executed file/script will be the file name of the generated OAR.
    /// 	If the process can't be spawned for some reason (file not found, no execute permission, etc), write a warning to the console.
    /// AutoBackupNaming: string. Default: Time.
    /// 	One of three strings (case insensitive):
    /// 	"Time": Current timestamp is appended to file name. An existing file will never be overwritten.
    ///  "Sequential": A number is appended to the file name. So if RegionName_x.oar exists, we'll save to RegionName_{x+1}.oar next. An existing file will never be overwritten.
    ///  "Overwrite": Always save to file named "${AutoBackupDir}/RegionName.oar", even if we have to overwrite an existing file.
    /// AutoBackupDir: String. Default: "." (the current directory).
    /// 	A directory (absolute or relative) where backups should be saved.
    /// AutoBackupDilationThreshold: float. Default: 0.5. Lower bound on time dilation required for BusyCheck heuristics to pass.
    ///  If the time dilation is below this value, don't take a backup right now.
    /// AutoBackupAgentThreshold: int. Default: 10. Upper bound on # of agents in region required for BusyCheck heuristics to pass.
    ///  If the number of agents is greater than this value, don't take a backup right now
    /// Save memory by setting low initial capacities. Minimizes impact in common cases of all regions using same interval, and instances hosting 1 ~ 4 regions.
    /// Also helps if you don't want AutoBackup at all.
    /// </remarks>
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "AutoBackupModule")]
    public class AutoBackupModule : ISharedRegionModule
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private readonly Dictionary<Guid, IScene> m_pendingSaves = new Dictionary<Guid, IScene>(1);
        private readonly AutoBackupModuleState m_defaultState = new AutoBackupModuleState();
        private readonly Dictionary<IScene, AutoBackupModuleState> m_states =
            new Dictionary<IScene, AutoBackupModuleState>(1);
        private readonly Dictionary<Timer, List<IScene>> m_timerMap =
            new Dictionary<Timer, List<IScene>>(1);
        private readonly Dictionary<double, Timer> m_timers = new Dictionary<double, Timer>(1);

        private delegate T DefaultGetter<T>(string settingName, T defaultValue);
        private bool m_enabled;
        private ICommandConsole m_console;
        private List<Scene> m_Scenes = new List<Scene> ();


        /// <summary>
        /// Whether the shared module should be enabled at all. NOT the same as m_Enabled in AutoBackupModuleState!
        /// </summary>
        private bool m_closed;

        private IConfigSource m_configSource;

        /// <summary>
        /// Required by framework.
        /// </summary>
        public bool IsSharedModule
        {
            get { return true; }
        }

        #region ISharedRegionModule Members

        /// <summary>
        /// Identifies the module to the system.
        /// </summary>
        string IRegionModuleBase.Name
        {
            get { return "AutoBackupModule"; }
        }

        /// <summary>
        /// We don't implement an interface, this is a single-use module.
        /// </summary>
        Type IRegionModuleBase.ReplaceableInterface
        {
            get { return null; }
        }

        /// <summary>
        /// Called once in the lifetime of the module at startup.
        /// </summary>
        /// <param name="source">The input config source for OpenSim.ini.</param>
        void IRegionModuleBase.Initialise(IConfigSource source)
        {
            // Determine if we have been enabled at all in OpenSim.ini -- this is part and parcel of being an optional module
            this.m_configSource = source;
            IConfig moduleConfig = source.Configs["AutoBackupModule"];
            if (moduleConfig == null)
            {
                this.m_enabled = false;
                return;
            }
            else
            {
                this.m_enabled = moduleConfig.GetBoolean("AutoBackupModuleEnabled", false);
                if (this.m_enabled)
                {
                    m_log.Info("[AUTO BACKUP]: AutoBackupModule enabled");
                }
                else
                {
                    return;
                }
            }

            Timer defTimer = new Timer(43200000);
            this.m_defaultState.Timer = defTimer;
            this.m_timers.Add(43200000, defTimer);
            defTimer.Elapsed += this.HandleElapsed;
            defTimer.AutoReset = true;
            defTimer.Start();

            AutoBackupModuleState abms = this.ParseConfig(null, true);
            m_log.Debug("[AUTO BACKUP]: Here is the default config:");
            m_log.Debug(abms.ToString());
        }

        /// <summary>
        /// Called once at de-init (sim shutting down).
        /// </summary>
        void IRegionModuleBase.Close()
        {
            if (!this.m_enabled)
            {
                return;
            }

            // We don't want any timers firing while the sim's coming down; strange things may happen.
            this.StopAllTimers();
        }

        /// <summary>
        /// Currently a no-op for AutoBackup because we have to wait for region to be fully loaded.
        /// </summary>
        /// <param name="scene"></param>
        void IRegionModuleBase.AddRegion (Scene scene)
        {
            if (!this.m_enabled) {
                return;
            }
            lock (m_Scenes) {
                m_Scenes.Add (scene);
            }
            m_console = MainConsole.Instance;

            m_console.Commands.AddCommand (
                "AutoBackup", false, "dobackup",
                "dobackup",
                "do backup.", DoBackup);
        }

        /// <summary>
        /// Here we just clean up some resources and stop the OAR backup (if any) for the given scene.
        /// </summary>
        /// <param name="scene">The scene (region) to stop performing AutoBackup on.</param>
        void IRegionModuleBase.RemoveRegion(Scene scene)
        {
            if (!this.m_enabled)
            {
                return;
            }
            m_Scenes.Remove (scene);
            if (this.m_states.ContainsKey(scene))
            {
                AutoBackupModuleState abms = this.m_states[scene];

                // Remove this scene out of the timer map list
                Timer timer = abms.Timer;
                List<IScene> list = this.m_timerMap[timer];
                list.Remove(scene);

                // Shut down the timer if this was the last scene for the timer
                if (list.Count == 0)
                {
                    this.m_timerMap.Remove(timer);
                    this.m_timers.Remove(timer.Interval);
                    timer.Close();
                }
                this.m_states.Remove(scene);
            }
        }

        /// <summary>
        /// Most interesting/complex code paths in AutoBackup begin here.
        /// We read lots of Nini config, maybe set a timer, add members to state tracking Dictionaries, etc.
        /// </summary>
        /// <param name="scene">The scene to (possibly) perform AutoBackup on.</param>
        void IRegionModuleBase.RegionLoaded(Scene scene)
        {
            if (!this.m_enabled)
            {
                return;
            }

            // This really ought not to happen, but just in case, let's pretend it didn't...
            if (scene == null)
            {
                return;
            }

            AutoBackupModuleState abms = this.ParseConfig(scene, false);
            m_log.Debug("[AUTO BACKUP]: Config for " + scene.RegionInfo.RegionName);
            m_log.Debug((abms == null ? "DEFAULT" : abms.ToString()));

            m_states.Add(scene, abms);
        }

        /// <summary>
        /// Currently a no-op.
        /// </summary>
        void ISharedRegionModule.PostInitialise()
        {
        }

        #endregion

        private void DoBackup (string module, string[] args)
        {
            if (args.Length != 2) {
                MainConsole.Instance.OutputFormat ("Usage: dobackup <regionname>");
                return;
            }
            bool found = false;
            string name = args [1];
            lock (m_Scenes) {
                foreach (Scene s in m_Scenes) {
                    string test = s.Name.ToString ();
                    if (test == name) {
                        found = true;
                        DoRegionBackup (s);
                    }
                }
                if (!found) {
                    MainConsole.Instance.OutputFormat ("No such region {0}. Nothing to backup", name);
                }
            }
        }

        /// <summary>
        /// Set up internal state for a given scene. Fairly complex code.
        /// When this method returns, we've started auto-backup timers, put members in Dictionaries, and created a State object for this scene.
        /// </summary>
        /// <param name="scene">The scene to look at.</param>
        /// <param name="parseDefault">Whether this call is intended to figure out what we consider the "default" config (applied to all regions unless overridden by per-region settings).</param>
        /// <returns>An AutoBackupModuleState contains most information you should need to know relevant to auto-backup, as applicable to a single region.</returns>
        private AutoBackupModuleState ParseConfig(IScene scene, bool parseDefault)
        {
            string sRegionName;
            string sRegionLabel;
//            string prepend;
            AutoBackupModuleState state;

            if (parseDefault)
            {
                sRegionName = null;
                sRegionLabel = "DEFAULT";
//                prepend = "";
                state = this.m_defaultState;
            }
            else
            {
                sRegionName = scene.RegionInfo.RegionName;
                sRegionLabel = sRegionName;
//                prepend = sRegionName + ".";
                state = null;
            }

            // Read the config settings and set variables.
            IConfig regionConfig = (scene != null ? scene.Config.Configs[sRegionName] : null);
            IConfig config = this.m_configSource.Configs["AutoBackupModule"];
            if (config == null)
            {
                // defaultState would be disabled too if the section doesn't exist.
                state = this.m_defaultState;
                return state;
            }

            bool tmpEnabled = ResolveBoolean("AutoBackup", this.m_defaultState.Enabled, config, regionConfig);
            if (state == null && tmpEnabled != this.m_defaultState.Enabled)
                //Varies from default state
            {
                state = new AutoBackupModuleState();
            }

            if (state != null)
            {
                state.Enabled = tmpEnabled;
            }

            // If you don't want AutoBackup, we stop.
            if ((state == null && !this.m_defaultState.Enabled) || (state != null && !state.Enabled))
            {
                return state;
            }
            else
            {
                m_log.Info("[AUTO BACKUP]: Region " + sRegionLabel + " is AutoBackup ENABLED.");
            }

            // Borrow an existing timer if one exists for the same interval; otherwise, make a new one.
            double interval =
                this.ResolveDouble("AutoBackupInterval", this.m_defaultState.IntervalMinutes,
                 config, regionConfig) * 60000.0;
            if (state == null && interval != this.m_defaultState.IntervalMinutes * 60000.0)
            {
                state = new AutoBackupModuleState();
            }

            if (this.m_timers.ContainsKey(interval))
            {
                if (state != null)
                {
                    state.Timer = this.m_timers[interval];
                }
                m_log.Debug("[AUTO BACKUP]: Reusing timer for " + interval + " msec for region " +
                            sRegionLabel);
            }
            else
            {
                // 0 or negative interval == do nothing.
                if (interval <= 0.0 && state != null)
                {
                    state.Enabled = false;
                    return state;
                }
                if (state == null)
                {
                    state = new AutoBackupModuleState();
                }
                Timer tim = new Timer(interval);
                state.Timer = tim;
                //Milliseconds -> minutes
                this.m_timers.Add(interval, tim);
                tim.Elapsed += this.HandleElapsed;
                tim.AutoReset = true;
                tim.Start();
            }

            // Add the current region to the list of regions tied to this timer.
            if (scene != null)
            {
                if (state != null)
                {
                    if (this.m_timerMap.ContainsKey(state.Timer))
                    {
                        this.m_timerMap[state.Timer].Add(scene);
                    }
                    else
                    {
                        List<IScene> scns = new List<IScene>(1);
                        scns.Add(scene);
                        this.m_timerMap.Add(state.Timer, scns);
                    }
                }
                else
                {
                    if (this.m_timerMap.ContainsKey(this.m_defaultState.Timer))
                    {
                        this.m_timerMap[this.m_defaultState.Timer].Add(scene);
                    }
                    else
                    {
                        List<IScene> scns = new List<IScene>(1);
                        scns.Add(scene);
                        this.m_timerMap.Add(this.m_defaultState.Timer, scns);
                    }
                }
            }

            bool tmpBusyCheck = ResolveBoolean("AutoBackupBusyCheck",
                                                  this.m_defaultState.BusyCheck, config, regionConfig);
            if (state == null && tmpBusyCheck != this.m_defaultState.BusyCheck)
            {
                state = new AutoBackupModuleState();
            }

            if (state != null)
            {
                state.BusyCheck = tmpBusyCheck;
            }

            // Included Option To Skip Assets
            bool tmpSkipAssets = ResolveBoolean("AutoBackupSkipAssets",
                                                  this.m_defaultState.SkipAssets, config, regionConfig);
            if (state == null && tmpSkipAssets != this.m_defaultState.SkipAssets)
            {
                state = new AutoBackupModuleState();
            }

            if (state != null)
            {
                state.SkipAssets = tmpSkipAssets;
            }

            // How long to keep backup files in days, 0 Disables this feature
            int tmpKeepFilesForDays = ResolveInt("AutoBackupKeepFilesForDays",
                                                  this.m_defaultState.KeepFilesForDays, config, regionConfig);
            if (state == null && tmpKeepFilesForDays != this.m_defaultState.KeepFilesForDays)
            {
                state = new AutoBackupModuleState();
            }

            if (state != null)
            {
                state.KeepFilesForDays = tmpKeepFilesForDays;
            }

            // Set file naming algorithm
            string stmpNamingType = ResolveString("AutoBackupNaming",
                                                     this.m_defaultState.NamingType.ToString(), config, regionConfig);
            NamingType tmpNamingType;
            if (stmpNamingType.Equals("Time", StringComparison.CurrentCultureIgnoreCase))
            {
                tmpNamingType = NamingType.Time;
            }
            else if (stmpNamingType.Equals("Sequential", StringComparison.CurrentCultureIgnoreCase))
            {
                tmpNamingType = NamingType.Sequential;
            }
            else if (stmpNamingType.Equals("Overwrite", StringComparison.CurrentCultureIgnoreCase))
            {
                tmpNamingType = NamingType.Overwrite;
            }
            else
            {
                m_log.Warn("Unknown naming type specified for region " + sRegionLabel + ": " +
                           stmpNamingType);
                tmpNamingType = NamingType.Time;
            }

            if (state == null && tmpNamingType != this.m_defaultState.NamingType)
            {
                state = new AutoBackupModuleState();
            }

            if (state != null)
            {
                state.NamingType = tmpNamingType;
            }

            string tmpScript = ResolveString("AutoBackupScript",
                                                this.m_defaultState.Script, config, regionConfig);
            if (state == null && tmpScript != this.m_defaultState.Script)
            {
                state = new AutoBackupModuleState();
            }

            if (state != null)
            {
                state.Script = tmpScript;
            }

            string tmpBackupDir = ResolveString("AutoBackupDir", ".", config, regionConfig);
            if (state == null && tmpBackupDir != this.m_defaultState.BackupDir)
            {
                state = new AutoBackupModuleState();
            }

            if (state != null)
            {
                state.BackupDir = tmpBackupDir;
                // Let's give the user some convenience and auto-mkdir
                if (state.BackupDir != ".")
                {
                    try
                    {
                        DirectoryInfo dirinfo = new DirectoryInfo(state.BackupDir);
                        if (!dirinfo.Exists)
                        {
                            dirinfo.Create();
                        }
                    }
                    catch (Exception e)
                    {
                        m_log.Warn(
                            "[AUTO BACKUP]: BAD NEWS. You won't be able to save backups to directory " +
                            state.BackupDir +
                            " because it doesn't exist or there's a permissions issue with it. Here's the exception.",
                            e);
                    }
                }
            }

            if(state == null)
                return m_defaultState;

            return state;
        }

        /// <summary>
        /// Helper function for ParseConfig.
        /// </summary>
        /// <param name="settingName"></param>
        /// <param name="defaultValue"></param>
        /// <param name="global"></param>
        /// <param name="local"></param>
        /// <returns></returns>
        private bool ResolveBoolean(string settingName, bool defaultValue, IConfig global, IConfig local)
        {
            if(local != null)
            {
                return local.GetBoolean(settingName, global.GetBoolean(settingName, defaultValue));
            }
            else
            {
                return global.GetBoolean(settingName, defaultValue);
            }
        }

        /// <summary>
        /// Helper function for ParseConfig.
        /// </summary>
        /// <param name="settingName"></param>
        /// <param name="defaultValue"></param>
        /// <param name="global"></param>
        /// <param name="local"></param>
        /// <returns></returns>
        private double ResolveDouble(string settingName, double defaultValue, IConfig global, IConfig local)
        {
            if (local != null)
            {
                return local.GetDouble(settingName, global.GetDouble(settingName, defaultValue));
            }
            else
            {
                return global.GetDouble(settingName, defaultValue);
            }
        }

        /// <summary>
        /// Helper function for ParseConfig.
        /// </summary>
        /// <param name="settingName"></param>
        /// <param name="defaultValue"></param>
        /// <param name="global"></param>
        /// <param name="local"></param>
        /// <returns></returns>
        private int ResolveInt(string settingName, int defaultValue, IConfig global, IConfig local)
        {
            if (local != null)
            {
                return local.GetInt(settingName, global.GetInt(settingName, defaultValue));
            }
            else
            {
                return global.GetInt(settingName, defaultValue);
            }
        }

        /// <summary>
        /// Helper function for ParseConfig.
        /// </summary>
        /// <param name="settingName"></param>
        /// <param name="defaultValue"></param>
        /// <param name="global"></param>
        /// <param name="local"></param>
        /// <returns></returns>
        private string ResolveString(string settingName, string defaultValue, IConfig global, IConfig local)
        {
            if (local != null)
            {
                return local.GetString(settingName, global.GetString(settingName, defaultValue));
            }
            else
            {
                return global.GetString(settingName, defaultValue);
            }
        }

        /// <summary>
        /// Called when any auto-backup timer expires. This starts the code path for actually performing a backup.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HandleElapsed(object sender, ElapsedEventArgs e)
        {
            // TODO: heuristic thresholds are per-region, so we should probably run heuristics once per region
            // XXX: Running heuristics once per region could add undue performance penalty for something that's supposed to
            // check whether the region is too busy! Especially on sims with LOTS of regions.
            // Alternative: make heuristics thresholds global to the module rather than per-region. Less flexible,
            //  but would allow us to be semantically correct while being easier on perf.
            // Alternative 2: Run heuristics once per unique set of heuristics threshold parameters! Ay yi yi...
            // Alternative 3: Don't support per-region heuristics at all; just accept them as a global only parameter.
            // Since this is pretty experimental, I haven't decided which alternative makes the most sense.
            if (this.m_closed)
            {
                return;
            }
            bool heuristicsRun = false;
            bool heuristicsPassed = false;
            if (!this.m_timerMap.ContainsKey((Timer) sender))
            {
                m_log.Debug("[AUTO BACKUP]: Code-up error: timerMap doesn't contain timer " + sender);
            }

            List<IScene> tmap = this.m_timerMap[(Timer) sender];
            if (tmap != null && tmap.Count > 0)
            {
                foreach (IScene scene in tmap)
                {
                    AutoBackupModuleState state = this.m_states[scene];
                    bool heuristics = state.BusyCheck;

                    // Fast path: heuristics are on; already ran em; and sim is fine; OR, no heuristics for the region.
                    if ((heuristics && heuristicsRun && heuristicsPassed) || !heuristics)
                    {
                        this.DoRegionBackup(scene);
                        // Heuristics are on; ran but we're too busy -- keep going. Maybe another region will have heuristics off!
                    }
                    else if (heuristicsRun)
                    {
                        m_log.Info("[AUTO BACKUP]: Heuristics: too busy to backup " +
                                   scene.RegionInfo.RegionName + " right now.");
                        continue;
                        // Logical Deduction: heuristics are on but haven't been run
                    }
                    else
                    {
                        heuristicsPassed = this.RunHeuristics(scene);
                        heuristicsRun = true;
                        if (!heuristicsPassed)
                        {
                            m_log.Info("[AUTO BACKUP]: Heuristics: too busy to backup " +
                                       scene.RegionInfo.RegionName + " right now.");
                            continue;
                        }
                        this.DoRegionBackup(scene);
                    }

                    // Remove Old Backups
                    this.RemoveOldFiles(state);
                }
            }
        }

        /// <summary>
        /// Save an OAR, register for the callback for when it's done, then call the AutoBackupScript (if applicable).
        /// </summary>
        /// <param name="scene"></param>
        private void DoRegionBackup(IScene scene)
        {
            if (!scene.Ready)
            {
                // We won't backup a region that isn't operating normally.
                m_log.Warn("[AUTO BACKUP]: Not backing up region " + scene.RegionInfo.RegionName +
                           " because its status is " + scene.RegionStatus);
                return;
            }

            AutoBackupModuleState state = this.m_states[scene];
            IRegionArchiverModule iram = scene.RequestModuleInterface<IRegionArchiverModule>();
            string savePath = BuildOarPath(scene.RegionInfo.RegionName,
                                                state.BackupDir,
                                                state.NamingType);
            if (savePath == null)
            {
                m_log.Warn("[AUTO BACKUP]: savePath is null in HandleElapsed");
                return;
            }
            Guid guid = Guid.NewGuid();
            m_pendingSaves.Add(guid, scene);
            state.LiveRequests.Add(guid, savePath);
            ((Scene) scene).EventManager.OnOarFileSaved += new EventManager.OarFileSaved(EventManager_OnOarFileSaved);

            m_log.Info("[AUTO BACKUP]: Backing up region " + scene.RegionInfo.RegionName);

            // Must pass options, even if dictionary is empty!
            Dictionary<string, object> options = new Dictionary<string, object>();

            if (state.SkipAssets)
                options["noassets"] = true;

            iram.ArchiveRegion(savePath, guid, options);
        }

        // For the given state, remove backup files older than the states KeepFilesForDays property
        private void RemoveOldFiles(AutoBackupModuleState state)
        {
            // 0 Means Disabled, Keep Files Indefinitely
            if (state.KeepFilesForDays > 0)
            {
                string[] files = Directory.GetFiles(state.BackupDir, "*.oar");
                DateTime CuttOffDate = DateTime.Now.AddDays(0 - state.KeepFilesForDays);

                foreach (string file in files)
                {
                    try
                    {
                        FileInfo fi = new FileInfo(file);
                        if (fi.CreationTime < CuttOffDate)
                            fi.Delete();
                    }
                    catch (Exception Ex)
                    {
                        m_log.Error("[AUTO BACKUP]: Error deleting old backup file '" + file + "': " + Ex.Message);
                    }
                }
            }
        }

        /// <summary>
        /// Called by the Event Manager when the OnOarFileSaved event is fired.
        /// </summary>
        /// <param name="guid"></param>
        /// <param name="message"></param>
        void EventManager_OnOarFileSaved(Guid guid, string message)
        {
            // Ignore if the OAR save is being done by some other part of the system
            if (m_pendingSaves.ContainsKey(guid))
            {
                AutoBackupModuleState abms = m_states[(m_pendingSaves[guid])];
                ExecuteScript(abms.Script, abms.LiveRequests[guid]);
                m_pendingSaves.Remove(guid);
                abms.LiveRequests.Remove(guid);
            }
        }

        /// <summary>This format may turn out to be too unwieldy to keep...
        /// Besides, that's what ctimes are for. But then how do I name each file uniquely without using a GUID?
        /// Sequential numbers, right? We support those, too!</summary>
        private static string GetTimeString()
        {
            StringWriter sw = new StringWriter();
            sw.Write("_");
            DateTime now = DateTime.Now;
            sw.Write(now.Year);
            sw.Write("y_");
            sw.Write(now.Month);
            sw.Write("M_");
            sw.Write(now.Day);
            sw.Write("d_");
            sw.Write(now.Hour);
            sw.Write("h_");
            sw.Write(now.Minute);
            sw.Write("m_");
            sw.Write(now.Second);
            sw.Write("s");
            sw.Flush();
            string output = sw.ToString();
            sw.Close();
            return output;
        }

        /// <summary>Return value of true ==> not too busy; false ==> too busy to backup an OAR right now, or error.</summary>
        private bool RunHeuristics(IScene region)
        {
            try
            {
                return this.RunTimeDilationHeuristic(region) && this.RunAgentLimitHeuristic(region);
            }
            catch (Exception e)
            {
                m_log.Warn("[AUTO BACKUP]: Exception in RunHeuristics", e);
                return false;
            }
        }

        /// <summary>
        /// If the time dilation right at this instant is less than the threshold specified in AutoBackupDilationThreshold (default 0.5),
        /// then we return false and trip the busy heuristic's "too busy" path (i.e. don't save an OAR).
        /// AutoBackupDilationThreshold is a _LOWER BOUND_. Lower Time Dilation is bad, so if you go lower than our threshold, it's "too busy".
        /// </summary>
        /// <param name="region"></param>
        /// <returns>Returns true if we're not too busy; false means we've got worse time dilation than the threshold.</returns>
        private bool RunTimeDilationHeuristic(IScene region)
        {
            string regionName = region.RegionInfo.RegionName;
            return region.TimeDilation >=
                   this.m_configSource.Configs["AutoBackupModule"].GetFloat(
                       regionName + ".AutoBackupDilationThreshold", 0.5f);
        }

        /// <summary>
        /// If the root agent count right at this instant is less than the threshold specified in AutoBackupAgentThreshold (default 10),
        /// then we return false and trip the busy heuristic's "too busy" path (i.e., don't save an OAR).
        /// AutoBackupAgentThreshold is an _UPPER BOUND_. Higher Agent Count is bad, so if you go higher than our threshold, it's "too busy".
        /// </summary>
        /// <param name="region"></param>
        /// <returns>Returns true if we're not too busy; false means we've got more agents on the sim than the threshold.</returns>
        private bool RunAgentLimitHeuristic(IScene region)
        {
            string regionName = region.RegionInfo.RegionName;
            try
            {
                Scene scene = (Scene) region;
                // TODO: Why isn't GetRootAgentCount() a method in the IScene interface? Seems generally useful...
                return scene.GetRootAgentCount() <=
                       this.m_configSource.Configs["AutoBackupModule"].GetInt(
                           regionName + ".AutoBackupAgentThreshold", 10);
            }
            catch (InvalidCastException ice)
            {
                m_log.Debug(
                    "[AUTO BACKUP]: I NEED MAINTENANCE: IScene is not a Scene; can't get root agent count!",
                    ice);
                return true;
                // Non-obstructionist safest answer...
            }
        }

        /// <summary>
        /// Run the script or executable specified by the "AutoBackupScript" config setting.
        /// Of course this is a security risk if you let anyone modify OpenSim.ini and they want to run some nasty bash script.
        /// But there are plenty of other nasty things that can be done with an untrusted OpenSim.ini, such as running high threat level scripting functions.
        /// </summary>
        /// <param name="scriptName"></param>
        /// <param name="savePath"></param>
        private static void ExecuteScript(string scriptName, string savePath)
        {
            // Do nothing if there's no script.
            if (scriptName == null || scriptName.Length <= 0)
            {
                return;
            }

            try
            {
                FileInfo fi = new FileInfo(scriptName);
                if (fi.Exists)
                {
                    ProcessStartInfo psi = new ProcessStartInfo(scriptName);
                    psi.Arguments = savePath;
                    psi.CreateNoWindow = true;
                    Process proc = Process.Start(psi);
                    proc.ErrorDataReceived += HandleProcErrorDataReceived;
                }
            }
            catch (Exception e)
            {
                m_log.Warn(
                    "Exception encountered when trying to run script for oar backup " + savePath, e);
            }
        }

        /// <summary>
        /// Called if a running script process writes to stderr.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void HandleProcErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            m_log.Warn("ExecuteScript hook " + ((Process) sender).ProcessName +
                       " is yacking on stderr: " + e.Data);
        }

        /// <summary>
        /// Quickly stop all timers from firing.
        /// </summary>
        private void StopAllTimers()
        {
            foreach (Timer t in this.m_timerMap.Keys)
            {
                t.Close();
            }
            this.m_closed = true;
        }

        /// <summary>
        /// Determine the next unique filename by number, for "Sequential" AutoBackupNamingType.
        /// </summary>
        /// <param name="dirName"></param>
        /// <param name="regionName"></param>
        /// <returns></returns>
        private static string GetNextFile(string dirName, string regionName)
        {
            FileInfo uniqueFile = null;
            long biggestExistingFile = GetNextOarFileNumber(dirName, regionName);
            biggestExistingFile++;
            // We don't want to overwrite the biggest existing file; we want to write to the NEXT biggest.
            uniqueFile =
                new FileInfo(dirName + Path.DirectorySeparatorChar + regionName + "_" +
                             biggestExistingFile + ".oar");
            return uniqueFile.FullName;
        }

        /// <summary>
        /// Top-level method for creating an absolute path to an OAR backup file based on what naming scheme the user wants.
        /// </summary>
        /// <param name="regionName">Name of the region to save.</param>
        /// <param name="baseDir">Absolute or relative path to the directory where the file should reside.</param>
        /// <param name="naming">The naming scheme for the file name.</param>
        /// <returns></returns>
        private static string BuildOarPath(string regionName, string baseDir, NamingType naming)
        {
            FileInfo path = null;
            switch (naming)
            {
                case NamingType.Overwrite:
                    path = new FileInfo(baseDir + Path.DirectorySeparatorChar + regionName + ".oar");
                    return path.FullName;
                case NamingType.Time:
                    path =
                        new FileInfo(baseDir + Path.DirectorySeparatorChar + regionName +
                                     GetTimeString() + ".oar");
                    return path.FullName;
                case NamingType.Sequential:
                    // All codepaths in GetNextFile should return a file name ending in .oar
                    path = new FileInfo(GetNextFile(baseDir, regionName));
                    return path.FullName;
                default:
                    m_log.Warn("VERY BAD: Unhandled case element " + naming);
                    break;
            }

            return null;
        }

        /// <summary>
        /// Helper function for Sequential file naming type (see BuildOarPath and GetNextFile).
        /// </summary>
        /// <param name="dirName"></param>
        /// <param name="regionName"></param>
        /// <returns></returns>
        private static long GetNextOarFileNumber(string dirName, string regionName)
        {
            long retval = 1;

            DirectoryInfo di = new DirectoryInfo(dirName);
            FileInfo[] fi = di.GetFiles(regionName, SearchOption.TopDirectoryOnly);
            Array.Sort(fi, (f1, f2) => StringComparer.CurrentCultureIgnoreCase.Compare(f1.Name, f2.Name));

            if (fi.LongLength > 0)
            {
                long subtract = 1L;
                bool worked = false;
                Regex reg = new Regex(regionName + "_([0-9])+" + ".oar");

                while (!worked && subtract <= fi.LongLength)
                {
                    // Pick the file with the last natural ordering
                    string biggestFileName = fi[fi.LongLength - subtract].Name;
                    MatchCollection matches = reg.Matches(biggestFileName);
                    long l = 1;
                    if (matches.Count > 0 && matches[0].Groups.Count > 0)
                    {
                        try
                        {
                            long.TryParse(matches[0].Groups[1].Value, out l);
                            retval = l;
                            worked = true;
                        }
                        catch (FormatException fe)
                        {
                            m_log.Warn(
                                "[AUTO BACKUP]: Error: Can't parse long value from file name to determine next OAR backup file number!",
                                fe);
                            subtract++;
                        }
                    }
                    else
                    {
                        subtract++;
                    }
                }
            }
            return retval;
        }
    }
}


