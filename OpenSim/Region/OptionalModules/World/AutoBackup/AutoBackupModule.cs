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
using System.IO;
using System.Timers;
using System.Diagnostics;
using System.Reflection;
using System.Collections.Generic;
using log4net;
using Nini;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Framework.Statistics;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;


/*
 * Config Settings Documentation.
 * At the TOP LEVEL, e.g. in OpenSim.ini, we have the following options:
 * In the [Modules] section:
 * 	AutoBackupModule: True/False. Default: False. If True, use the auto backup module. Otherwise it will be disabled regardless of what settings are in Regions.ini!
 * EACH REGION, in OpenSim.ini, can have the following settings under the [AutoBackupModule] section.
 * VERY IMPORTANT: You must create the key name as follows: <Region Name>.<Key Name>
 * Example: My region is named Foo.
 * If I wanted to specify the "AutoBackupInterval" key below, I would name my key "Foo.AutoBackupInterval", under the [AutoBackupModule] section of OpenSim.ini.
 * Instead of specifying them on a per-region basis, you can also omit the region name to specify the default setting for all regions.
 * Region-specific settings take precedence.
 * AutoBackup: True/False. Default: False. If True, activate auto backup functionality. 
 * 	This is the only required option for enabling auto-backup; the other options have sane defaults. 
 * 	If False, the auto-backup module becomes a no-op for the region, and all other AutoBackup* settings are ignored.
 * AutoBackupInterval: Double, non-negative value. Default: 720 (12 hours). 
 * 	The number of minutes between each backup attempt. 
 * 	If a negative or zero value is given, it is equivalent to setting AutoBackup = False.
 * AutoBackupBusyCheck: True/False. Default: True. 
 * 	If True, we will only take an auto-backup if a set of conditions are met. 
 * 	These conditions are heuristics to try and avoid taking a backup when the sim is busy.
 * AutoBackupScript: String. Default: not specified (disabled). 
 * 	File path to an executable script or binary to run when an automatic backup is taken.
 *  The file should really be (Windows) an .exe or .bat, or (Linux/Mac) a shell script or binary.
 *  Trying to "run" directories, or things with weird file associations on Win32, might cause unexpected results!
 * 	argv[1] of the executed file/script will be the file name of the generated OAR. 
 * 	If the process can't be spawned for some reason (file not found, no execute permission, etc), write a warning to the console.
 * AutoBackupNaming: string. Default: Time.
 * 	One of three strings (case insensitive):
 * 	"Time": Current timestamp is appended to file name. An existing file will never be overwritten.
 *  "Sequential": A number is appended to the file name. So if RegionName_x.oar exists, we'll save to RegionName_{x+1}.oar next. An existing file will never be overwritten.
 *  "Overwrite": Always save to file named "${AutoBackupDir}/RegionName.oar", even if we have to overwrite an existing file.
 * AutoBackupDir: String. Default: "." (the current directory).
 * 	A directory (absolute or relative) where backups should be saved.
 * AutoBackupDilationThreshold: float. Default: 0.5. Lower bound on time dilation required for BusyCheck heuristics to pass.
 *  If the time dilation is below this value, don't take a backup right now.
 * AutoBackupAgentThreshold: int. Default: 10. Upper bound on # of agents in region required for BusyCheck heuristics to pass.
 *  If the number of agents is greater than this value, don't take a backup right now.
 * */

namespace OpenSim.Region.OptionalModules.World.AutoBackup
{

	public enum NamingType
	{
		TIME,
		SEQUENTIAL,
		OVERWRITE
	}

	public class AutoBackupModule : ISharedRegionModule, IRegionModuleBase
	{

		private static readonly ILog m_log = LogManager.GetLogger (MethodBase.GetCurrentMethod ().DeclaringType);

		//AutoBackupModuleState: Auto-Backup state for one region (scene).
		public class AutoBackupModuleState
		{
			private bool m_enabled = false;
			private NamingType m_naming = NamingType.TIME;
			private Timer m_timer = null;
			private bool m_busycheck = true;
			private string m_script = null;
			private string m_dir = ".";

			public AutoBackupModuleState ()
			{

			}

			public void SetEnabled (bool b)
			{
				m_enabled = b;
			}

			public bool GetEnabled ()
			{
				return m_enabled;
			}

			public Timer GetTimer ()
			{
				return m_timer;
			}
			
			public double GetIntervalMinutes ()
			{
			    if(m_timer == null)
			    {
			        return -1.0;
			    }
			    else
			    {
			        return m_timer.Interval / 60000.0;
			    }
			}

			public void SetTimer (Timer t)
			{
				m_timer = t;
			}

			public bool GetBusyCheck ()
			{
				return m_busycheck;
			}

			public void SetBusyCheck (bool b)
			{
				m_busycheck = b;
			}


			public string GetScript ()
			{
				return m_script;
			}

			public void SetScript (string s)
			{
				m_script = s;
			}

			public string GetBackupDir ()
			{
				return m_dir;
			}

			public void SetBackupDir (string s)
			{
				m_dir = s;
			}

			public NamingType GetNamingType ()
			{
				return m_naming;
			}

			public void SetNamingType (NamingType n)
			{
				m_naming = n;
			}
			
			public string ToString()
			{
			    string retval = "";
			    
			    retval += "[AUTO BACKUP]: AutoBackup: " + (GetEnabled() ? "ENABLED" : "DISABLED") + "\n";
			    retval += "[AUTO BACKUP]: Interval: " + GetIntervalMinutes() + " minutes" + "\n";
			    retval += "[AUTO BACKUP]: Do Busy Check: " + (GetBusyCheck() ? "Yes" : "No") + "\n";
			    retval += "[AUTO BACKUP]: Naming Type: " + GetNamingType().ToString() + "\n";
			    retval += "[AUTO BACKUP]: Backup Dir: " + GetBackupDir() + "\n";
			    retval += "[AUTO BACKUP]: Script: " + GetScript() + "\n";
			    return retval;
			}
		}

		//Save memory by setting low initial capacities. Minimizes impact in common cases of all regions using same interval, and instances hosting 1 ~ 4 regions.
		//Also helps if you don't want AutoBackup at all
		readonly Dictionary<IScene, AutoBackupModuleState> states = new Dictionary<IScene, AutoBackupModuleState> (4);
		readonly Dictionary<double, Timer> timers = new Dictionary<double, Timer> (1);
		readonly Dictionary<Timer, List<IScene>> timerMap = new Dictionary<Timer, List<IScene>> (1);
		private IConfigSource m_configSource = null;
		private bool m_Enabled = false;
		//Whether the shared module should be enabled at all. NOT the same as m_Enabled in AutoBackupModuleState!
		private bool m_closed = false;
		//True means IRegionModuleBase.Close() was called on us, and we should stop operation ASAP.
		//Used to prevent elapsing timers after Close() is called from trying to start an autobackup while the sim is shutting down.
		readonly AutoBackupModuleState defaultState = new AutoBackupModuleState();
		
		public AutoBackupModule ()
		{
			
		}

		#region IRegionModuleBase implementation
		void IRegionModuleBase.Initialise (Nini.Config.IConfigSource source)
		{
			//Determine if we have been enabled at all in OpenSim.ini -- this is part and parcel of being an optional module
			m_configSource = source;
			IConfig moduleConfig = source.Configs["Modules"];
			if (moduleConfig != null) {
				m_Enabled = moduleConfig.GetBoolean ("AutoBackupModule", false);
				if (m_Enabled) {
					m_log.Info ("[AUTO BACKUP]: AutoBackupModule enabled");
				}
			}
			
			Timer defTimer = new Timer(720 * 60000);
			defaultState.SetTimer(defTimer);
			timers.Add (720*60000, defTimer);
			defTimer.Elapsed += HandleElapsed;
			defTimer.AutoReset = true;
			defTimer.Start ();
			
			AutoBackupModuleState abms = ParseConfig(null, false);
			m_log.Debug("[AUTO BACKUP]: Config for default");
			m_log.Debug(abms.ToString());
		}

		void IRegionModuleBase.Close ()
		{
			if (!m_Enabled)
				return;
			
			//We don't want any timers firing while the sim's coming down; strange things may happen.
			StopAllTimers ();
		}

		void IRegionModuleBase.AddRegion (Framework.Scenes.Scene scene)
		{
			//NO-OP. Wait for the region to be loaded.
		}

		void IRegionModuleBase.RemoveRegion (Framework.Scenes.Scene scene)
		{
			if (!m_Enabled)
				return;
			
			AutoBackupModuleState abms = states[scene];
			Timer timer = abms.GetTimer ();
			List<IScene> list = timerMap[timer];
			list.Remove (scene);
			if (list.Count == 0) {
				timerMap.Remove (timer);
				timers.Remove (timer.Interval);
				timer.Close ();
			}
			states.Remove(scene);
		}

		void IRegionModuleBase.RegionLoaded (Framework.Scenes.Scene scene)
		{
			if (!m_Enabled)
				return;
			
			//This really ought not to happen, but just in case, let's pretend it didn't...
			if (scene == null)
				return;
					
			AutoBackupModuleState abms = ParseConfig(scene, true);
			m_log.Debug("[AUTO BACKUP]: Config for " + scene.RegionInfo.RegionName);
			m_log.Debug(abms.ToString());
		}
		
		AutoBackupModuleState ParseConfig (IScene scene, bool parseDefault)
		{
		    string sRegionName;
		    string sRegionLabel;
		    string prepend;
		    AutoBackupModuleState state;
		    
		    if(parseDefault)
		    {
		        sRegionName = null;
		        sRegionLabel = "DEFAULT";
		        prepend = "";
		        state = defaultState;
		    }
		    else
		    {
		        sRegionName = scene.RegionInfo.RegionName;
		        sRegionLabel = sRegionName;
		        prepend = sRegionName + ".";
		        state = null;
		    }
		    
		    //Read the config settings and set variables.
			IConfig config = m_configSource.Configs["AutoBackupModule"];
			if (config == null) {
			    state = defaultState; //defaultState would be disabled too if the section doesn't exist.
				m_log.Info ("[AUTO BACKUP]: Region " + sRegionLabel + " is NOT AutoBackup enabled.");
				return state;
			}
			
			bool tmpEnabled = config.GetBoolean (prepend + "AutoBackup", defaultState.GetEnabled());
			if(state == null && tmpEnabled != defaultState.GetEnabled()) //Varies from default state
			{
			    state = new AutoBackupModuleState();
			    state.SetEnabled (tmpEnabled);
			}
			
			//If you don't want AutoBackup, we stop.
			if ((state == null && !defaultState.GetEnabled()) || !state.GetEnabled ()) {
				m_log.Info ("[AUTO BACKUP]: Region " + sRegionLabel + " is NOT AutoBackup enabled.");
				state = defaultState;
				return state;
			} else {
				m_log.Info ("[AUTO BACKUP]: Region " + sRegionLabel + " is AutoBackup ENABLED.");
			}
			
			//Borrow an existing timer if one exists for the same interval; otherwise, make a new one.
			double interval = config.GetDouble (prepend + "AutoBackupInterval", defaultState.GetIntervalMinutes()) * 60000.0;
		    if(state == null && interval != defaultState.GetIntervalMinutes() * 60000.0)
		    {
		        state = new AutoBackupModuleState();
	        }
	        
			if (timers.ContainsKey (interval)) {
			    if(state != null)
    			    state.SetTimer (timers[interval]);
				m_log.Debug ("[AUTO BACKUP]: Reusing timer for " + interval + " msec for region " + sRegionLabel);
			} else {
				//0 or negative interval == do nothing.
				if (interval <= 0.0 && state != null) {
					state.SetEnabled (false);
					return state;
				}
				if(state == null)
				    state = new AutoBackupModuleState();
				Timer tim = new Timer (interval);
				state.SetTimer (tim);
				//Milliseconds -> minutes
				timers.Add (interval, tim);
				tim.Elapsed += HandleElapsed;
				tim.AutoReset = true;
				tim.Start ();
				//m_log.Debug("[AUTO BACKUP]: New timer for " + interval + " msec for region " + sRegionName);
			}
			
			//Add the current region to the list of regions tied to this timer.
			if(state != null)
			{
			    if (timerMap.ContainsKey (state.GetTimer ())) {
				    timerMap[state.GetTimer ()].Add (scene);
			    } else {
				    List<IScene> scns = new List<IScene> (1);
				    scns.Add (scene);
				    timerMap.Add (state.GetTimer (), scns);
			    }
			}
			else
			{
			    if(timerMap.ContainsKey(defaultState.GetTimer())) {
			        timerMap[defaultState.GetTimer()].Add(scene);
			    } else {
			        List<IScene> scns = new List<IScene> (1);
			        scns.Add(scene);
			        timerMap.Add(defaultState.GetTimer(), scns);
			    }
			}
			
			bool tmpBusyCheck = config.GetBoolean (prepend + "AutoBackupBusyCheck", defaultState.GetBusyCheck());
			if(state == null && tmpBusyCheck != defaultState.GetBusyCheck())
			{
			    state = new AutoBackupModuleState();
			}
			
			if(state != null)
			{
    			state.SetBusyCheck (tmpBusyCheck);
			}
			
			//Set file naming algorithm
			string stmpNamingType = config.GetString (prepend + "AutoBackupNaming", defaultState.GetNamingType().ToString());
			NamingType tmpNamingType;
			if (stmpNamingType.Equals ("Time", StringComparison.CurrentCultureIgnoreCase)) {
				tmpNamingType = NamingType.TIME;
			} else if (stmpNamingType.Equals ("Sequential", StringComparison.CurrentCultureIgnoreCase)) {
				tmpNamingType = NamingType.SEQUENTIAL;
			} else if (stmpNamingType.Equals ("Overwrite", StringComparison.CurrentCultureIgnoreCase)) {
				tmpNamingType = NamingType.OVERWRITE;
			} else {
				m_log.Warn ("Unknown naming type specified for region " + sRegionLabel + ": " + stmpNamingType);
                tmpNamingType = NamingType.TIME;
			}
			
			if(state == null && tmpNamingType != defaultState.GetNamingType())
			{
			    state = new AutoBackupModuleState();
			}
			
			if(state != null)
			{
			    state.SetNamingType(tmpNamingType);
			}
			
			string tmpScript = config.GetString (prepend + "AutoBackupScript", defaultState.GetScript());
			if(state == null && tmpScript != defaultState.GetScript())
			{
			    state = new AutoBackupModuleState();
			}
			
			if(state != null)
			{
			    state.SetScript (tmpScript);
			}
			
			string tmpBackupDir = config.GetString (prepend + "AutoBackupDir", ".");
			if(state == null && tmpBackupDir != defaultState.GetBackupDir())
			{
			    state = new AutoBackupModuleState();
			}
			
			if(state != null)
			{
			    state.SetBackupDir (tmpBackupDir);
			    //Let's give the user *one* convenience and auto-mkdir
			    if (state.GetBackupDir () != ".") {
				    try {
					    DirectoryInfo dirinfo = new DirectoryInfo (state.GetBackupDir ());
					    if (!dirinfo.Exists) {
						    dirinfo.Create ();
					    }
				    } catch (Exception e) {
					    m_log.Warn ("BAD NEWS. You won't be able to save backups to directory " + state.GetBackupDir () + " because it doesn't exist or there's a permissions issue with it. Here's the exception.", e);
				    }
			    }
			}
			
			return state;
		}

		void HandleElapsed (object sender, ElapsedEventArgs e)
		{
			//TODO?: heuristic thresholds are per-region, so we should probably run heuristics once per region
			//XXX: Running heuristics once per region could add undue performance penalty for something that's supposed to
			//check whether the region is too busy! Especially on sims with LOTS of regions.
			//Alternative: make heuristics thresholds global to the module rather than per-region. Less flexible,
			// but would allow us to be semantically correct while being easier on perf.
			//Alternative 2: Run heuristics once per unique set of heuristics threshold parameters! Ay yi yi...
			if (m_closed)
				return;
			bool heuristicsRun = false;
			bool heuristicsPassed = false;
			if (!timerMap.ContainsKey ((Timer)sender)) {
				m_log.Debug ("Code-up error: timerMap doesn't contain timer " + sender.ToString ());
			}
			
			List<IScene> tmap = timerMap[(Timer)sender];
			if(tmap != null && tmap.Count > 0)
			foreach (IScene scene in tmap) {
				AutoBackupModuleState state = states[scene];
				bool heuristics = state.GetBusyCheck ();
				
				//Fast path: heuristics are on; already ran em; and sim is fine; OR, no heuristics for the region.
				if ((heuristics && heuristicsRun && heuristicsPassed) || !heuristics) {
					doRegionBackup (scene);
					//Heuristics are on; ran but we're too busy -- keep going. Maybe another region will have heuristics off!
				} else if (heuristics && heuristicsRun && !heuristicsPassed) {
					m_log.Info ("[AUTO BACKUP]: Heuristics: too busy to backup " + scene.RegionInfo.RegionName + " right now.");
					continue;
					//Logical Deduction: heuristics are on but haven't been run
				} else {
					heuristicsPassed = RunHeuristics (scene);
					heuristicsRun = true;
					if (!heuristicsPassed) {
						m_log.Info ("[AUTO BACKUP]: Heuristics: too busy to backup " + scene.RegionInfo.RegionName + " right now.");
						continue;
					}
					doRegionBackup (scene);
				}
			}
		}

		void doRegionBackup (IScene scene)
		{
			if (scene.RegionStatus != RegionStatus.Up) {
				//We won't backup a region that isn't operating normally.
				m_log.Warn ("[AUTO BACKUP]: Not backing up region " + scene.RegionInfo.RegionName + " because its status is " + scene.RegionStatus.ToString ());
				return;
			}
			
			AutoBackupModuleState state = states[scene];
			IRegionArchiverModule iram = scene.RequestModuleInterface<IRegionArchiverModule> ();
			string savePath = BuildOarPath (scene.RegionInfo.RegionName, state.GetBackupDir (), state.GetNamingType ());
			//m_log.Debug("[AUTO BACKUP]: savePath = " + savePath);
			if (savePath == null) {
				m_log.Warn ("[AUTO BACKUP]: savePath is null in HandleElapsed");
				return;
			}
			iram.ArchiveRegion (savePath, null);
			ExecuteScript (state.GetScript (), savePath);
		}

		string IRegionModuleBase.Name {
			get { return "AutoBackupModule"; }
		}

		Type IRegionModuleBase.ReplaceableInterface {
			get { return null; }
		}

		#endregion
		#region ISharedRegionModule implementation
		void ISharedRegionModule.PostInitialise ()
		{
			//I don't care right now.
		}

		#endregion

		//Is this even needed?
		public bool IsSharedModule {
			get { return true; }
		}

		private string BuildOarPath (string regionName, string baseDir, NamingType naming)
		{
			FileInfo path = null;
			switch (naming) {
			case NamingType.OVERWRITE:
				path = new FileInfo (baseDir + Path.DirectorySeparatorChar + regionName);
				return path.FullName;
			case NamingType.TIME:
				path = new FileInfo (baseDir + Path.DirectorySeparatorChar + regionName + GetTimeString () + ".oar");
				return path.FullName;
			case NamingType.SEQUENTIAL:
				path = new FileInfo (GetNextFile (baseDir, regionName));
				return path.FullName;
			default:
				m_log.Warn ("VERY BAD: Unhandled case element " + naming.ToString ());
				break;
			}
			
			return path.FullName;
		}

		//Welcome to the TIME STRING. 4 CORNER INTEGERS, CUBES 4 QUAD MEMORY -- No 1 Integer God.
		//(Terrible reference to <timecube.com>)
		//This format may turn out to be too unwieldy to keep...
		//Besides, that's what ctimes are for. But then how do I name each file uniquely without using a GUID?
		//Sequential numbers, right? Ugh. Almost makes TOO much sense.
		private string GetTimeString ()
		{
			StringWriter sw = new StringWriter ();
			sw.Write ("_");
			DateTime now = DateTime.Now;
			sw.Write (now.Year);
			sw.Write ("y_");
			sw.Write (now.Month);
			sw.Write ("M_");
			sw.Write (now.Day);
			sw.Write ("d_");
			sw.Write (now.Hour);
			sw.Write ("h_");
			sw.Write (now.Minute);
			sw.Write ("m_");
			sw.Write (now.Second);
			sw.Write ("s");
			sw.Flush ();
			string output = sw.ToString ();
			sw.Close ();
			return output;
		}

		//Get the next logical file name
		//I really shouldn't put fields here, but for now.... ;)
		private string m_dirName = null;
		private string m_regionName = null;
		private string GetNextFile (string dirName, string regionName)
		{
			FileInfo uniqueFile = null;
			m_dirName = dirName;
			m_regionName = regionName;
			long biggestExistingFile = HalfIntervalMaximize (1, FileExistsTest);
			biggestExistingFile++;
			//We don't want to overwrite the biggest existing file; we want to write to the NEXT biggest.
			uniqueFile = new FileInfo (m_dirName + Path.DirectorySeparatorChar + m_regionName + "_" + biggestExistingFile + ".oar");
			if (uniqueFile.Exists) {
				//Congratulations, your strange deletion patterns fooled my half-interval search into picking an existing file!
				//Now you get to pay the performance cost :)
				uniqueFile = UniqueFileSearchLinear (biggestExistingFile);
			}
			
			return uniqueFile.FullName;
		}

		/*
		 * Return value of true ==> not too busy; false ==> too busy to backup an OAR right now, or error.
		 * */
		private bool RunHeuristics (IScene region)
		{
			try {
				return RunTimeDilationHeuristic (region) && RunAgentLimitHeuristic (region);
			} catch (Exception e) {
				m_log.Warn ("[AUTO BACKUP]: Exception in RunHeuristics", e);
				return false;
			}
		}

		/*
		 * If the time dilation right at this instant is less than the threshold specified in AutoBackupDilationThreshold (default 0.5),
		 * then we return false and trip the busy heuristic's "too busy" path (i.e. don't save an OAR).
		 * AutoBackupDilationThreshold is a _LOWER BOUND_. Lower Time Dilation is bad, so if you go lower than our threshold, it's "too busy".
		 * Return value of "true" ==> not too busy. Return value of "false" ==> too busy!
		 * */
		private bool RunTimeDilationHeuristic (IScene region)
		{
			string regionName = region.RegionInfo.RegionName;
			return region.TimeDilation >= m_configSource.Configs["AutoBackupModule"].GetFloat (regionName + ".AutoBackupDilationThreshold", 0.5f);
		}

		/*
		 * If the root agent count right at this instant is less than the threshold specified in AutoBackupAgentThreshold (default 10),
		 * then we return false and trip the busy heuristic's "too busy" path (i.e., don't save an OAR).
		 * AutoBackupAgentThreshold is an _UPPER BOUND_. Higher Agent Count is bad, so if you go higher than our threshold, it's "too busy".
		 * Return value of "true" ==> not too busy. Return value of "false" ==> too busy!
		 * */
		private bool RunAgentLimitHeuristic (IScene region)
		{
			string regionName = region.RegionInfo.RegionName;
			try {
				Scene scene = (Scene)region;
				//TODO: Why isn't GetRootAgentCount() a method in the IScene interface? Seems generally useful...
				return scene.GetRootAgentCount () <= m_configSource.Configs["AutoBackupModule"].GetInt (regionName + ".AutoBackupAgentThreshold", 10);
			} catch (InvalidCastException ice) {
				m_log.Debug ("[AUTO BACKUP]: I NEED MAINTENANCE: IScene is not a Scene; can't get root agent count!");
				return true;
				//Non-obstructionist safest answer...
			}
		}

		private void ExecuteScript (string scriptName, string savePath)
		{
			//Fast path out
			if (scriptName == null || scriptName.Length <= 0)
				return;
			
			try {
				FileInfo fi = new FileInfo (scriptName);
				if (fi.Exists) {
					ProcessStartInfo psi = new ProcessStartInfo (scriptName);
					psi.Arguments = savePath;
					psi.CreateNoWindow = true;
					Process proc = Process.Start (psi);
					proc.ErrorDataReceived += HandleProcErrorDataReceived;
				}
			} catch (Exception e) {
				m_log.Warn ("Exception encountered when trying to run script for oar backup " + savePath, e);
			}
		}

		void HandleProcErrorDataReceived (object sender, DataReceivedEventArgs e)
		{
			m_log.Warn ("ExecuteScript hook " + ((Process)sender).ProcessName + " is yacking on stderr: " + e.Data);
		}

		private void StopAllTimers ()
		{
			foreach (Timer t in timerMap.Keys) {
				t.Close ();
			}
			m_closed = true;
		}

		/* Find the largest value for which the predicate returns true.
		 * We use a bisection algorithm (half interval) to make the algorithm scalable.
		 * The worst-case complexity is about O(log(n)^2) in practice.
		 * Only for extremely small values (under 10) do you notice it taking more iterations than a linear search.
		 * The number of predicate invocations only hits a few hundred when the maximized value
		 *     is in the tens of millions, so prepare for the predicate to be invoked between 10 and 100 times.
		 * And of course it is fantastic with powers of 2, which are densely packed in values under 100 anyway.
		 * The Predicate<long> parameter must be a function that accepts a long and returns a bool.
		 * */
		public long HalfIntervalMaximize (long start, Predicate<long> pred)
		{
			long prev = start, curr = start, biggest = 0;
			
			if (start < 0)
				throw new IndexOutOfRangeException ("Start value for HalfIntervalMaximize must be non-negative");
			
			do {
				if (pred (curr)) {
					if (curr > biggest) {
						biggest = curr;
					}
					prev = curr;
					if (curr == 0) {
						//Special case because 0 * 2 = 0 :)
						curr = 1;
					} else {
						//Look deeper
						curr *= 2;
					}
				} else {
					// We went too far, back off halfway
					curr = (curr + prev) / 2;
				}
			} while (curr - prev > 0);
			
			return biggest;
		}

		public bool FileExistsTest (long num)
		{
			FileInfo test = new FileInfo (m_dirName + Path.DirectorySeparatorChar + m_regionName + "_" + num + ".oar");
			return test.Exists;
		}


		//Very slow, hence why we try the HalfIntervalMaximize first!
		public FileInfo UniqueFileSearchLinear (long start)
		{
			long l = start;
			FileInfo retval = null;
			do {
				retval = new FileInfo (m_dirName + Path.DirectorySeparatorChar + m_regionName + "_" + (l++) + ".oar");
			} while (retval.Exists);
			
			return retval;
		}
	}
	
}

