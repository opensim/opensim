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
    /// Configuration setting can be specified in two places: OpenSim.ini and/or Regions.ini.
    ///
    /// OpenSim.ini only settings section [AutoBackupModule]
    /// AutoBackupModuleEnabled: True/False. Default: False. If True, use the auto backup module.
    ///     if false module is disable and all rest is ignored
    /// AutoBackupInterval: Double, non-negative value. Default: 720 (12 hours).
    /// 	The number of minutes between each backup attempt.
    /// AutoBackupDir: String. Default: "." (the current directory).
    /// 	A directory (absolute or relative) where backups should be saved.
    /// AutoBackupKeepFilesForDays remove files older than this number of days. 0  disables
    /// 
    /// Next can be set on OpenSim.ini, as default, and or per region in Regions.ini
    /// Region-specific settings take precedence.
    /// 
    /// AutoBackup: True/False. Default: False. If True, activate auto backup functionality.
    ///     controls backup per region, with default optionaly set on OpenSim.ini
    
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
    ///     "Sequential": A number is appended to the file name. So if RegionName_x.oar exists, we'll save to RegionName_{x+1}.oar next. An existing file will never be overwritten.
    ///     "Overwrite": Always save to file named "${AutoBackupDir}/RegionName.oar", even if we have to overwrite an existing file.
    /// </remarks>
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "AutoBackupModule")]
    public class AutoBackupModule : ISharedRegionModule
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private readonly AutoBackupModuleState m_defaultState = new AutoBackupModuleState();
        private readonly Dictionary<IScene, AutoBackupModuleState> m_states =
            new Dictionary<IScene, AutoBackupModuleState>(1);

        private delegate T DefaultGetter<T>(string settingName, T defaultValue);
        private bool m_enabled;
        private ICommandConsole m_console;
        private List<Scene> m_Scenes = new List<Scene> ();
        private Timer m_masterTimer;
        private bool m_busy;
        private int m_KeepFilesForDays = -1;
        private string m_backupDir;
        private bool m_doneFirst;
        private double m_baseInterval;

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
        public string  Name
        {
            get { return "AutoBackupModule"; }
        }

        /// <summary>
        /// We don't implement an interface, this is a single-use module.
        /// </summary>
        public Type ReplaceableInterface
        {
            get { return null; }
        }

        /// <summary>
        /// Called once in the lifetime of the module at startup.
        /// </summary>
        /// <param name="source">The input config source for OpenSim.ini.</param>
        public void Initialise(IConfigSource source)
        {
            // Determine if we have been enabled at all in OpenSim.ini -- this is part and parcel of being an optional module
            m_configSource = source;
            IConfig moduleConfig = source.Configs["AutoBackupModule"];
            if (moduleConfig == null)
            {
                m_enabled = false;
                return;
            }

            m_enabled = moduleConfig.GetBoolean("AutoBackupModuleEnabled", false);
            if(!m_enabled)
                return;

            ParseDefaultConfig(moduleConfig);
            if(!m_enabled)
                return;

            m_log.Debug("[AUTO BACKUP]: Default config:");
            m_log.Debug(m_defaultState.ToString());

            m_log.Info("[AUTO BACKUP]: AutoBackupModule enabled");
            m_masterTimer = new Timer();
            m_masterTimer.Interval = m_baseInterval;
            m_masterTimer.Elapsed += HandleElapsed;
            m_masterTimer.AutoReset = false;

            m_console = MainConsole.Instance;

            m_console.Commands.AddCommand (
                        "AutoBackup", true, "dooarbackup",
                        "dooarbackup <regionName> | ALL",
                        "saves the single region <regionName> to a oar or ALL regions in instance to oars, using same settings as AutoBackup. Note it restarts time interval", DoBackup);
            m_busy = true;            
        }

        /// <summary>
        /// Called once at de-init (sim shutting down).
        /// </summary>
        public void Close()
        {
            if (!m_enabled)
                return;

            // We don't want any timers firing while the sim's coming down; strange things may happen.
            m_masterTimer.Dispose();
        }

        /// <summary>
        /// Currently a no-op for AutoBackup because we have to wait for region to be fully loaded.
        /// </summary>
        /// <param name="scene"></param>
        public void AddRegion (Scene scene)
        {
            if (!m_enabled)
                return;

            lock (m_Scenes)
                m_Scenes.Add (scene);
        }

        /// <summary>
        /// Here we just clean up some resources and stop the OAR backup (if any) for the given scene.
        /// </summary>
        /// <param name="scene">The scene (region) to stop performing AutoBackup on.</param>
        public void RemoveRegion(Scene scene)
        {
            if (m_enabled)
                return;

            lock(m_Scenes)
            {
                if (m_states.ContainsKey(scene))
                    m_states.Remove(scene);
                m_Scenes.Remove(scene);
            }
        }

        /// <summary>
        /// Most interesting/complex code paths in AutoBackup begin here.
        /// We read lots of Nini config, maybe set a timer, add members to state tracking Dictionaries, etc.
        /// </summary>
        /// <param name="scene">The scene to (possibly) perform AutoBackup on.</param>
        public void RegionLoaded(Scene scene)
        {
            if (!m_enabled)
                return;

            // This really ought not to happen, but just in case, let's pretend it didn't...
            if (scene == null)
                return;

            AutoBackupModuleState abms = ParseConfig(scene);
            if(abms == null)
            {
                m_log.Debug("[AUTO BACKUP]: Config for " + scene.RegionInfo.RegionName);
                m_log.Debug("DEFAULT");
                abms = new AutoBackupModuleState(m_defaultState);
            }
            else
            {
                m_log.Debug("[AUTO BACKUP]: Config for " + scene.RegionInfo.RegionName);
                m_log.Debug(abms.ToString());
            }

            m_states.Add(scene, abms);
            m_busy = false;
            m_masterTimer.Start();
        }

        /// <summary>
        /// Currently a no-op.
        /// </summary>
        public void PostInitialise()
        {
        }

        #endregion

        private void DoBackup (string module, string[] args)
        {
            if (!m_enabled)
                return;

            if (args.Length != 2)
            {
                MainConsole.Instance.Output("Usage: dooarbackup <regionname>");
                return;
            }

            if(m_busy)
            {
                MainConsole.Instance.Output("Already doing a backup, please try later");
                return;
            }

            m_masterTimer.Stop();
            m_busy = true;    

            bool found = false;
            string name = args [1];
            Scene[] scenes;
            lock (m_Scenes)
                scenes = m_Scenes.ToArray();

            if(scenes == null)
                return;

            Scene s;
            try
            {
                if(name == "ALL")
                {
                    for(int i = 0; i < scenes.Length; i++)
                    {
                        s = scenes[i];
                        DoRegionBackup(s);
                        if (!m_enabled)
                            return;
                    }
                    return;
                }

                for(int i = 0; i < scenes.Length; i++)
                {
                    s = scenes[i];
                    if (s.Name == name)
                    {
                        found = true;
                        DoRegionBackup(s);
                        break;
                    }
                }
            }
            catch { }
            finally
            {
                if (m_enabled)
                    m_masterTimer.Start();
                m_busy = false;
            }               
            if (!found)
                    MainConsole.Instance.Output("No such region {0}. Nothing to backup", name);
        }

        private void ParseDefaultConfig(IConfig config)
        {          

            m_backupDir = ".";
            string backupDir = config.GetString("AutoBackupDir", ".");
            if (backupDir != ".")
            {
                try
                {
                DirectoryInfo dirinfo = new DirectoryInfo(backupDir);
                if (!dirinfo.Exists)
                    dirinfo.Create();
                }
                catch (Exception e)
                {
                    m_enabled = false;
                    m_log.WarnFormat("[AUTO BACKUP]: Error accessing backup folder {0}. Module disabled. {1}",
                            backupDir, e);
                    return;
                }
            }
            m_backupDir = backupDir;

            double interval = config.GetDouble("AutoBackupInterval", 720);
            interval *= 60000.0;
            m_baseInterval = interval;

            // How long to keep backup files in days, 0 Disables this feature
            m_KeepFilesForDays = config.GetInt("AutoBackupKeepFilesForDays",m_KeepFilesForDays);

            m_defaultState.Enabled = config.GetBoolean("AutoBackup", m_defaultState.Enabled);

            m_defaultState.SkipAssets = config.GetBoolean("AutoBackupSkipAssets",m_defaultState.SkipAssets);

            // Set file naming algorithm
            string stmpNamingType = config.GetString("AutoBackupNaming", m_defaultState.NamingType.ToString());
            NamingType tmpNamingType;
            if (stmpNamingType.Equals("Time", StringComparison.CurrentCultureIgnoreCase))
                tmpNamingType = NamingType.Time;
            else if (stmpNamingType.Equals("Sequential", StringComparison.CurrentCultureIgnoreCase))
                tmpNamingType = NamingType.Sequential;
            else if (stmpNamingType.Equals("Overwrite", StringComparison.CurrentCultureIgnoreCase))
                tmpNamingType = NamingType.Overwrite;
            else
            {
                m_log.Warn("Unknown naming type specified for Default");
                tmpNamingType = NamingType.Time;
            }
            m_defaultState.NamingType = tmpNamingType;

            m_defaultState.Script = config.GetString("AutoBackupScript", m_defaultState.Script);

        }

        /// <summary>
        /// Set up internal state for a given scene. Fairly complex code.
        /// When this method returns, we've started auto-backup timers, put members in Dictionaries, and created a State object for this scene.
        /// </summary>
        /// <param name="scene">The scene to look at.</param>
        /// <param name="parseDefault">Whether this call is intended to figure out what we consider the "default" config (applied to all regions unless overridden by per-region settings).</param>
        /// <returns>An AutoBackupModuleState contains most information you should need to know relevant to auto-backup, as applicable to a single region.</returns>
        private AutoBackupModuleState ParseConfig(IScene scene)
        {
            if(scene == null)
                return null;

            string sRegionName;
            AutoBackupModuleState state = null;

            sRegionName = scene.RegionInfo.RegionName;

            // Read the config settings and set variables.
            IConfig regionConfig = scene.Config.Configs[sRegionName];
            if (regionConfig == null)
                return null;

            state = new AutoBackupModuleState();

            state.Enabled = regionConfig.GetBoolean("AutoBackup", m_defaultState.Enabled);

            // Included Option To Skip Assets
            state.SkipAssets = regionConfig.GetBoolean("AutoBackupSkipAssets", m_defaultState.SkipAssets);

            // Set file naming algorithm
            string stmpNamingType = regionConfig.GetString("AutoBackupNaming", m_defaultState.NamingType.ToString());
            NamingType tmpNamingType;
            if (stmpNamingType.Equals("Time", StringComparison.CurrentCultureIgnoreCase))
                tmpNamingType = NamingType.Time;
            else if (stmpNamingType.Equals("Sequential", StringComparison.CurrentCultureIgnoreCase))
                tmpNamingType = NamingType.Sequential;
            else if (stmpNamingType.Equals("Overwrite", StringComparison.CurrentCultureIgnoreCase))
                tmpNamingType = NamingType.Overwrite;
            else
            {
                m_log.Warn("Unknown naming type specified for region " + sRegionName + ": " +
                           stmpNamingType);
                tmpNamingType = NamingType.Time;
            }
            m_defaultState.NamingType = tmpNamingType;

            state.Script = regionConfig.GetString("AutoBackupScript", m_defaultState.Script);
            return state;
        }


        /// <summary>
        /// Called when any auto-backup timer expires. This starts the code path for actually performing a backup.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HandleElapsed(object sender, ElapsedEventArgs e)
        {
            if (!m_enabled || m_busy)
                return;

            m_busy = true;
            if(m_doneFirst && m_KeepFilesForDays > 0)
                RemoveOldFiles();

            foreach (IScene scene in m_Scenes)
            {
                if (!m_enabled)
                    return;
                DoRegionBackup(scene);
            }

            if (m_enabled)
            {
                m_masterTimer.Start();
                m_busy = false;
            }

            m_doneFirst = true;
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

            m_busy = true;

            AutoBackupModuleState state;
            if(!m_states.TryGetValue(scene, out state))
                return;

            if(state == null || !state.Enabled)
                return;

            IRegionArchiverModule iram = scene.RequestModuleInterface<IRegionArchiverModule>();
            if(iram == null)
                return;

            string savePath = BuildOarPath(scene.RegionInfo.RegionName,
                                                m_backupDir,
                                                state.NamingType);
            if (savePath == null)
            {
                m_log.Warn("[AUTO BACKUP]: savePath is null in HandleElapsed");
                return;
            }

            Guid guid = Guid.NewGuid();
            m_log.Info("[AUTO BACKUP]: Backing up region " + scene.RegionInfo.RegionName);

            // Must pass options, even if dictionary is empty!
            Dictionary<string, object> options = new Dictionary<string, object>();

            if (state.SkipAssets)
                options["noassets"] = true;

            iram.ArchiveRegion(savePath, guid, options);
            ExecuteScript(state.Script, savePath);
        }

        // For the given state, remove backup files older than the states KeepFilesForDays property
        private void RemoveOldFiles()
        {
            string[] files;
            try
            {
                files = Directory.GetFiles(m_backupDir, "*.oar");
            }
            catch (Exception Ex)
            {
                m_log.Error("[AUTO BACKUP]: Error reading backup folder " + m_backupDir + ": " + Ex.Message);
                return;
            }

            DateTime CuttOffDate = DateTime.Now.AddDays(-m_KeepFilesForDays);

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
