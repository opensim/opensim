#pragma warning disable 1587
/// 
/// Copyright (c) Contributors, http://opensimulator.org/
/// See CONTRIBUTORS.TXT for a full list of copyright holders.
/// 
/// Redistribution and use in source and binary forms, with or without
/// modification, are permitted provided that the following conditions are met:
///     * Redistributions of source code must retain the above copyright
///       notice, this list of conditions and the following disclaimer.
///     * Redistributions in binary form must reproduce the above copyright
///       notice, this list of conditions and the following disclaimer in the
///       documentation and/or other materials provided with the distribution.
///     * Neither the name of the OpenSimulator Project nor the
///       names of its contributors may be used to endorse or promote products
///       derived from this software without specific prior written permission.
///
/// THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
/// EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
/// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
/// DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
/// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
/// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
/// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
/// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
/// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
/// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
///

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Timers;
using System.Text.RegularExpressions;
using log4net;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

///
/// Config Settings Documentation.
/// At the TOP LEVEL, e.g. in OpenSim.ini, we have the following options:
/// EACH REGION, in OpenSim.ini, can have the following settings under the [AutoBackupModule] section.
/// IMPORTANT: You may optionally specify the key name as follows for a per-region key: <Region Name>.<Key Name>
/// Example: My region is named Foo.
/// If I wanted to specify the "AutoBackupInterval" key just for this region, I would name my key "Foo.AutoBackupInterval", under the [AutoBackupModule] section of OpenSim.ini.
/// Instead of specifying them on a per-region basis, you can also omit the region name to specify the default setting for all regions.
/// Region-specific settings take precedence.
/// 
/// AutoBackupModuleEnabled: True/False. Default: False. If True, use the auto backup module. This setting does not support per-region basis.
///     All other settings under [AutoBackupModule] are ignored if AutoBackupModuleEnabled is false, even per-region settings!
/// AutoBackup: True/False. Default: False. If True, activate auto backup functionality. 
/// 	This is the only required option for enabling auto-backup; the other options have sane defaults. 
/// 	If False for a particular region, the auto-backup module becomes a no-op for the region, and all other AutoBackup* settings are ignored.
/// 	If False globally (the default), only regions that specifically override this with "FooRegion.AutoBackup = true" will get AutoBackup functionality.
/// AutoBackupInterval: Double, non-negative value. Default: 720 (12 hours). 
/// 	The number of minutes between each backup attempt. 
/// 	If a negative or zero value is given, it is equivalent to setting AutoBackup = False.
/// AutoBackupBusyCheck: True/False. Default: True. 
/// 	If True, we will only take an auto-backup if a set of conditions are met. 
/// 	These conditions are heuristics to try and avoid taking a backup when the sim is busy.
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
///  If the number of agents is greater than this value, don't take a backup right now.
///

namespace OpenSim.Region.OptionalModules.World.AutoBackup
{
    public enum NamingType
    {
        Time,
        Sequential,
        Overwrite
    }

    public class AutoBackupModule : ISharedRegionModule
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// True means IRegionModuleBase.Close() was called on us, and we should stop operation ASAP.
        /// Used to prevent elapsing timers after Close() is called from trying to start an autobackup while the sim is shutting down.
        private readonly AutoBackupModuleState m_defaultState = new AutoBackupModuleState();

        /// Save memory by setting low initial capacities. Minimizes impact in common cases of all regions using same interval, and instances hosting 1 ~ 4 regions.
        /// Also helps if you don't want AutoBackup at all
        private readonly Dictionary<IScene, AutoBackupModuleState> m_states =
            new Dictionary<IScene, AutoBackupModuleState>(1);

        private readonly Dictionary<Timer, List<IScene>> m_timerMap =
            new Dictionary<Timer, List<IScene>>(1);

        private readonly Dictionary<double, Timer> m_timers = new Dictionary<double, Timer>(1);

        private bool m_enabled;

        /// Whether the shared module should be enabled at all. NOT the same as m_Enabled in AutoBackupModuleState!
        private bool m_closed;

        private IConfigSource m_configSource;

        public bool IsSharedModule
        {
            get { return true; }
        }

        #region ISharedRegionModule Members

        string IRegionModuleBase.Name
        {
            get { return "AutoBackupModule"; }
        }

        Type IRegionModuleBase.ReplaceableInterface
        {
            get { return null; }
        }

        void IRegionModuleBase.Initialise(IConfigSource source)
        {
            /// Determine if we have been enabled at all in OpenSim.ini -- this is part and parcel of being an optional module
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

        void IRegionModuleBase.Close()
        {
            if (!this.m_enabled)
            {
                return;
            }

            /// We don't want any timers firing while the sim's coming down; strange things may happen.
            this.StopAllTimers();
        }

        void IRegionModuleBase.AddRegion(Scene scene)
        {
            /// NO-OP. Wait for the region to be loaded.
        }

        void IRegionModuleBase.RemoveRegion(Scene scene)
        {
            if (!this.m_enabled)
            {
                return;
            }

            if (this.m_states.ContainsKey(scene))
            {
                AutoBackupModuleState abms = this.m_states[scene];

                /// Remove this scene out of the timer map list
                Timer timer = abms.Timer;
                List<IScene> list = this.m_timerMap[timer];
                list.Remove(scene);

                /// Shut down the timer if this was the last scene for the timer
                if (list.Count == 0)
                {
                    this.m_timerMap.Remove(timer);
                    this.m_timers.Remove(timer.Interval);
                    timer.Close();
                }
                this.m_states.Remove(scene);
            }
        }

        void IRegionModuleBase.RegionLoaded(Scene scene)
        {
            if (!this.m_enabled)
            {
                return;
            }

            /// This really ought not to happen, but just in case, let's pretend it didn't...
            if (scene == null)
            {
                return;
            }

            AutoBackupModuleState abms = this.ParseConfig(scene, false);
            m_log.Debug("[AUTO BACKUP]: Config for " + scene.RegionInfo.RegionName);
            m_log.Debug((abms == null ? "DEFAULT" : abms.ToString()));
        }

        void ISharedRegionModule.PostInitialise()
        {
            /// I don't care right now.
        }

        #endregion

        private AutoBackupModuleState ParseConfig(IScene scene, bool parseDefault)
        {
            string sRegionName;
            string sRegionLabel;
            string prepend;
            AutoBackupModuleState state;

            if (parseDefault)
            {
                sRegionName = null;
                sRegionLabel = "DEFAULT";
                prepend = "";
                state = this.m_defaultState;
            }
            else
            {
                sRegionName = scene.RegionInfo.RegionName;
                sRegionLabel = sRegionName;
                prepend = sRegionName + ".";
                state = null;
            }

            /// Read the config settings and set variables.
            IConfig config = this.m_configSource.Configs["AutoBackupModule"];
            if (config == null)
            {
                /// defaultState would be disabled too if the section doesn't exist.
                state = this.m_defaultState;
                m_log.Info("[AUTO BACKUP]: Region " + sRegionLabel + " is NOT AutoBackup enabled.");
                return state;
            }

            bool tmpEnabled = config.GetBoolean(prepend + "AutoBackup", this.m_defaultState.Enabled);
            if (state == null && tmpEnabled != this.m_defaultState.Enabled)
                //Varies from default state
            {
                state = new AutoBackupModuleState();
            }

            if (state != null)
            {
                state.Enabled = tmpEnabled;
            }

            /// If you don't want AutoBackup, we stop.
            if ((state == null && !this.m_defaultState.Enabled) || (state != null && !state.Enabled))
            {
                m_log.Info("[AUTO BACKUP]: Region " + sRegionLabel + " is NOT AutoBackup enabled.");
                return state;
            }
            else
            {
                m_log.Info("[AUTO BACKUP]: Region " + sRegionLabel + " is AutoBackup ENABLED.");
            }

            /// Borrow an existing timer if one exists for the same interval; otherwise, make a new one.
            double interval =
                config.GetDouble(prepend + "AutoBackupInterval", this.m_defaultState.IntervalMinutes)*
                60000.0;
            if (state == null && interval != this.m_defaultState.IntervalMinutes*60000.0)
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
                /// 0 or negative interval == do nothing.
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

            /// Add the current region to the list of regions tied to this timer.
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

            bool tmpBusyCheck = config.GetBoolean(prepend + "AutoBackupBusyCheck",
                                                  this.m_defaultState.BusyCheck);
            if (state == null && tmpBusyCheck != this.m_defaultState.BusyCheck)
            {
                state = new AutoBackupModuleState();
            }

            if (state != null)
            {
                state.BusyCheck = tmpBusyCheck;
            }

            /// Set file naming algorithm
            string stmpNamingType = config.GetString(prepend + "AutoBackupNaming",
                                                     this.m_defaultState.NamingType.ToString());
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

            string tmpScript = config.GetString(prepend + "AutoBackupScript",
                                                this.m_defaultState.Script);
            if (state == null && tmpScript != this.m_defaultState.Script)
            {
                state = new AutoBackupModuleState();
            }

            if (state != null)
            {
                state.Script = tmpScript;
            }

            string tmpBackupDir = config.GetString(prepend + "AutoBackupDir", ".");
            if (state == null && tmpBackupDir != this.m_defaultState.BackupDir)
            {
                state = new AutoBackupModuleState();
            }

            if (state != null)
            {
                state.BackupDir = tmpBackupDir;
                /// Let's give the user *one* convenience and auto-mkdir
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
                            "BAD NEWS. You won't be able to save backups to directory " +
                            state.BackupDir +
                            " because it doesn't exist or there's a permissions issue with it. Here's the exception.",
                            e);
                    }
                }
            }

            return state;
        }

        private void HandleElapsed(object sender, ElapsedEventArgs e)
        {
            /// TODO?: heuristic thresholds are per-region, so we should probably run heuristics once per region
            /// XXX: Running heuristics once per region could add undue performance penalty for something that's supposed to
            /// check whether the region is too busy! Especially on sims with LOTS of regions.
            /// Alternative: make heuristics thresholds global to the module rather than per-region. Less flexible,
            ///  but would allow us to be semantically correct while being easier on perf.
            /// Alternative 2: Run heuristics once per unique set of heuristics threshold parameters! Ay yi yi...
            if (this.m_closed)
            {
                return;
            }
            bool heuristicsRun = false;
            bool heuristicsPassed = false;
            if (!this.m_timerMap.ContainsKey((Timer) sender))
            {
                m_log.Debug("Code-up error: timerMap doesn't contain timer " + sender);
            }

            List<IScene> tmap = this.m_timerMap[(Timer) sender];
            if (tmap != null && tmap.Count > 0)
            {
                foreach (IScene scene in tmap)
                {
                    AutoBackupModuleState state = this.m_states[scene];
                    bool heuristics = state.BusyCheck;

                    /// Fast path: heuristics are on; already ran em; and sim is fine; OR, no heuristics for the region.
                    if ((heuristics && heuristicsRun && heuristicsPassed) || !heuristics)
                    {
                        this.DoRegionBackup(scene);
                        /// Heuristics are on; ran but we're too busy -- keep going. Maybe another region will have heuristics off!
                    }
                    else if (heuristicsRun)
                    {
                        m_log.Info("[AUTO BACKUP]: Heuristics: too busy to backup " +
                                   scene.RegionInfo.RegionName + " right now.");
                        continue;
                        /// Logical Deduction: heuristics are on but haven't been run
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
                }
            }
        }

        private void DoRegionBackup(IScene scene)
        {
            if (scene.RegionStatus != RegionStatus.Up)
            {
                /// We won't backup a region that isn't operating normally.
                m_log.Warn("[AUTO BACKUP]: Not backing up region " + scene.RegionInfo.RegionName +
                           " because its status is " + scene.RegionStatus);
                return;
            }

            AutoBackupModuleState state = this.m_states[scene];
            IRegionArchiverModule iram = scene.RequestModuleInterface<IRegionArchiverModule>();
            string savePath = BuildOarPath(scene.RegionInfo.RegionName,
                                                state.BackupDir,
                                                state.NamingType);
            /// m_log.Debug("[AUTO BACKUP]: savePath = " + savePath);
            if (savePath == null)
            {
                m_log.Warn("[AUTO BACKUP]: savePath is null in HandleElapsed");
                return;
            }
            iram.ArchiveRegion(savePath, Guid.NewGuid(), null);
            ExecuteScript(state.Script, savePath);
        }

        /// This format may turn out to be too unwieldy to keep...
        /// Besides, that's what ctimes are for. But then how do I name each file uniquely without using a GUID?
        /// Sequential numbers, right? Ugh. Almost makes TOO much sense.
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

        ///
        /// Return value of true ==> not too busy; false ==> too busy to backup an OAR right now, or error.
        ///
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

        ///
        /// If the time dilation right at this instant is less than the threshold specified in AutoBackupDilationThreshold (default 0.5),
        /// then we return false and trip the busy heuristic's "too busy" path (i.e. don't save an OAR).
        /// AutoBackupDilationThreshold is a _LOWER BOUND_. Lower Time Dilation is bad, so if you go lower than our threshold, it's "too busy".
        /// Return value of "true" ==> not too busy. Return value of "false" ==> too busy!
        ///
        private bool RunTimeDilationHeuristic(IScene region)
        {
            string regionName = region.RegionInfo.RegionName;
            return region.TimeDilation >=
                   this.m_configSource.Configs["AutoBackupModule"].GetFloat(
                       regionName + ".AutoBackupDilationThreshold", 0.5f);
        }

        ///
        /// If the root agent count right at this instant is less than the threshold specified in AutoBackupAgentThreshold (default 10),
        /// then we return false and trip the busy heuristic's "too busy" path (i.e., don't save an OAR).
        /// AutoBackupAgentThreshold is an _UPPER BOUND_. Higher Agent Count is bad, so if you go higher than our threshold, it's "too busy".
        /// Return value of "true" ==> not too busy. Return value of "false" ==> too busy!
        ///
        private bool RunAgentLimitHeuristic(IScene region)
        {
            string regionName = region.RegionInfo.RegionName;
            try
            {
                Scene scene = (Scene) region;
                /// TODO: Why isn't GetRootAgentCount() a method in the IScene interface? Seems generally useful...
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
                /// Non-obstructionist safest answer...
            }
        }

        private static void ExecuteScript(string scriptName, string savePath)
        {
            //Fast path out
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

        private static void HandleProcErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            m_log.Warn("ExecuteScript hook " + ((Process) sender).ProcessName +
                       " is yacking on stderr: " + e.Data);
        }

        private void StopAllTimers()
        {
            foreach (Timer t in this.m_timerMap.Keys)
            {
                t.Close();
            }
            this.m_closed = true;
        }

        private static string GetNextFile(string dirName, string regionName)
        {
            FileInfo uniqueFile = null;
            long biggestExistingFile = GetNextOarFileNumber(dirName, regionName);
            biggestExistingFile++;
            //We don't want to overwrite the biggest existing file; we want to write to the NEXT biggest.
            uniqueFile =
                new FileInfo(dirName + Path.DirectorySeparatorChar + regionName + "_" +
                             biggestExistingFile + ".oar");
            return uniqueFile.FullName;
        }

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
                    /// All codepaths in GetNextFile should return a file name ending in .oar
                    path = new FileInfo(GetNextFile(baseDir, regionName));
                    return path.FullName;
                default:
                    m_log.Warn("VERY BAD: Unhandled case element " + naming);
                    break;
            }

            return null;
        }

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
                    /// Pick the file with the last natural ordering
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


