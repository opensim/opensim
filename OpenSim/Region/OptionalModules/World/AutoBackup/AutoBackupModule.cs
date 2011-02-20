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
using OpenSim.Region.Framework.Interfaces;


/*
 * Config Settings Documentation.
 * At the TOP LEVEL, e.g. in OpenSim.ini, we have one option:
 * In the [Modules] section:
 * 	AutoBackupModule: True/False. Default: False. If True, use the auto backup module. Otherwise it will be disabled regardless of what settings are in Regions.ini!
 * EACH REGION in e.g. Regions/Regions.ini can have the following options:
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
 * */

namespace OpenSim.Region.OptionalModules.World.AutoBackup
{
	
	public enum NamingType
	{
		TIME,
		SEQUENTIAL,
		OVERWRITE
	};
	
	public class AutoBackupModule : ISharedRegionModule, IRegionModuleBase
	{
		
		private static readonly ILog m_log = 
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
		
		//AutoBackupModuleState: Auto-Backup state for one region (scene).
		public class AutoBackupModuleState
		{
			private readonly IScene m_scene;
			private bool m_enabled = false;
			private NamingType m_naming = NamingType.TIME;
			private Timer m_timer = null;
			private bool m_busycheck = true;
			private string m_script = null;
			private string m_dir = ".";
			
			public AutoBackupModuleState(IScene scene)
			{
				m_scene = scene;
				if(scene == null)
					throw new NullReferenceException("Required parameter missing for AutoBackupModuleState constructor");
			}
			
			public void SetEnabled(bool b)
			{
				m_enabled = b;	
			}
			
			public bool GetEnabled()
			{
				return m_enabled;	
			}
			
			public Timer GetTimer()
			{
				return m_timer;	
			}
			
			public void SetTimer(Timer t)
			{
				m_timer = t;	
			}
			
			public bool GetBusyCheck()
			{
				return m_busycheck;	
			}
			
			public void SetBusyCheck(bool b)
			{
				m_busycheck = b;	
			}
			
			
			public string GetScript()
			{
				return m_script;	
			}
			
			public void SetScript(string s)
			{
				m_script = s;	
			}
			
			public string GetBackupDir()
			{
				return m_dir;	
			}
			
			public void SetBackupDir(string s)
			{
				m_dir = s;	
			}
			
			public NamingType GetNamingType()
			{
				return m_naming;
			}
			
			public void SetNamingType(NamingType n)
			{
				m_naming = n;	
			}
		}
		
		//Save memory by setting low initial capacities. Minimizes impact in common cases of all regions using same interval, and instances hosting 1 ~ 4 regions.
		//Also helps if you don't want AutoBackup at all
		readonly Dictionary<IScene, AutoBackupModuleState> states = new Dictionary<IScene, AutoBackupModuleState>(4);
		readonly Dictionary<double, Timer> timers = new Dictionary<double, Timer>(1);
		readonly Dictionary<Timer, List<IScene>> timerMap = new Dictionary<Timer, List<IScene>>(1);
		private bool m_Enabled = false; //Whether the shared module should be enabled at all. NOT the same as m_Enabled in AutoBackupModuleState!
		
		public AutoBackupModule ()
		{
			
		}

		#region IRegionModuleBase implementation
		void IRegionModuleBase.Initialise (Nini.Config.IConfigSource source)
		{
			//Determine if we have been enabled at all in OpenSim.ini -- this is part and parcel of being an optional module
            IConfig moduleConfig = source.Configs["Modules"];
            if (moduleConfig != null)
            {
                m_Enabled = moduleConfig.GetBoolean("AutoBackupModule", false);
                if (m_Enabled)
                {
                    m_log.Info("[AUTO BACKUP MODULE]: AutoBackupModule enabled");
                }

            }
		}

		void IRegionModuleBase.Close ()
		{
			if(!m_Enabled)
				return;
			
			//We don't want any timers firing while the sim's coming down; strange things may happen.
			StopAllTimers();
		}

		void IRegionModuleBase.AddRegion (Framework.Scenes.Scene scene)
		{
			//NO-OP. Wait for the region to be loaded.
		}

		void IRegionModuleBase.RemoveRegion (Framework.Scenes.Scene scene)
		{
			if(!m_Enabled)
				return;
			
			AutoBackupModuleState abms = states[scene];
			Timer timer = abms.GetTimer();
			List<IScene> list = timerMap[timer];
			list.Remove(scene);
			if(list.Count == 0)
			{
				timerMap.Remove(timer);
				timers.Remove(timer.Interval);
				timer.Close();
			}
		}

		void IRegionModuleBase.RegionLoaded (Framework.Scenes.Scene scene)
		{
			if(!m_Enabled)
				return;
			
			//This really ought not to happen, but just in case, let's pretend it didn't...
			if(scene == null)
				return;
			
			AutoBackupModuleState st = new AutoBackupModuleState(scene);
			states.Add(scene, st);
			
			//Read the config settings and set variables.
			IConfig config = scene.Config.Configs[scene.RegionInfo.RegionName];
			st.SetEnabled(config.GetBoolean("AutoBackup", false));
			if(!st.GetEnabled()) //If you don't want AutoBackup, we stop.
				return;
			
			//Borrow an existing timer if one exists for the same interval; otherwise, make a new one.
			double interval = config.GetDouble("AutoBackupInterval", 720);
			if(timers.ContainsKey(interval))
			{
				st.SetTimer(timers[interval]);
			}
			else
			{
				st.SetTimer(new Timer(interval));
				timers.Add(interval, st.GetTimer());
				st.GetTimer().Elapsed += HandleElapsed;
			}
			
			//Add the current region to the list of regions tied to this timer.
			if(timerMap.ContainsKey(st.GetTimer()))
			{
				timerMap[st.GetTimer()].Add(scene);
			}
			else
			{
				List<IScene> scns = new List<IScene>(1);
				timerMap.Add(st.GetTimer(), scns);
			}
			
			st.SetBusyCheck(config.GetBoolean("AutoBackupBusyCheck", true));
			
			//Set file naming algorithm
			string namingtype = config.GetString("AutoBackupNaming", "Time");
			if(namingtype.Equals("Time", StringComparison.CurrentCultureIgnoreCase))
			{
				st.SetNamingType(NamingType.TIME);
			}
			else if(namingtype.Equals("Sequential", StringComparison.CurrentCultureIgnoreCase))
			{
				st.SetNamingType(NamingType.SEQUENTIAL);	
			}
			else if(namingtype.Equals("Overwrite", StringComparison.CurrentCultureIgnoreCase))
			{
				st.SetNamingType(NamingType.OVERWRITE);	
			}
			else
			{
				m_log.Warn("Unknown naming type specified for region " + scene.RegionInfo.RegionName + ": " + namingtype);
				st.SetNamingType(NamingType.TIME);
			}
			
			st.SetScript(config.GetString("AutoBackupScript", null));
			st.SetBackupDir(config.GetString("AutoBackupDir", "."));
			
			//Let's give the user *one* convenience and auto-mkdir
			if(st.GetBackupDir() != ".")
			{
				try
				{
					DirectoryInfo dirinfo = new DirectoryInfo(st.GetBackupDir());
					if(!dirinfo.Exists)
					{
						dirinfo.Create();	
					}
				}
				catch(Exception e)
				{
					m_log.Warn("BAD NEWS. You won't be able to save backups to directory " + st.GetBackupDir() +
					           " because it doesn't exist or there's a permissions issue with it. Here's the exception.", e);
				}
			}
		}

		void HandleElapsed (object sender, ElapsedEventArgs e)
		{
			bool heuristicsRun = false;
			bool heuristicsPassed = false;
			foreach(IScene scene in timerMap[(Timer)sender])
			{
				AutoBackupModuleState state = states[scene];
				bool heuristics = state.GetBusyCheck();
				
				//Fast path: heuristics are on; already ran em; and sim is fine; OR, no heuristics for the region.
				if((heuristics && heuristicsRun && heuristicsPassed)
				   || !heuristics)
				{
					IRegionArchiverModule iram = scene.RequestModuleInterface<IRegionArchiverModule>();
					string savePath = BuildOarPath(scene.RegionInfo.RegionName, state.GetBackupDir(), state.GetNamingType());
					if(savePath == null)
					{
						m_log.Warn("savePath is null in HandleElapsed");
						continue;
					}
					iram.ArchiveRegion(savePath, null);
					ExecuteScript(state.GetScript(), savePath);
				}
				//Heuristics are on; ran but we're too busy -- keep going. Maybe another region will have heuristics off!
				else if(heuristics && heuristicsRun && !heuristicsPassed)
				{
					continue;
				}
				//Logical Deduction: heuristics are on but haven't been run
				else
				{
					heuristicsPassed = RunHeuristics();
					heuristicsRun = true;
					if(!heuristicsPassed)
						continue;
				}
			}
		}

		string IRegionModuleBase.Name {
			get {
				return "AutoBackupModule";
			}
		}

		Type IRegionModuleBase.ReplaceableInterface {
			get {
				return null;
			}
		}
			                     
		#endregion
		#region ISharedRegionModule implementation
		void ISharedRegionModule.PostInitialise ()
		{
			//I don't care right now.
		}
		
		#endregion
		
		//Is this even needed?
		public bool IsSharedModule
        {
            get { return true; }
        }
		
		private string BuildOarPath(string regionName, string baseDir, NamingType naming)
		{
			FileInfo path = null;
			switch(naming)
			{
			case NamingType.OVERWRITE:
				path = new FileInfo(baseDir + Path.DirectorySeparatorChar + regionName);
				return path.FullName;
			case NamingType.TIME:
				path = new FileInfo(baseDir + Path.DirectorySeparatorChar + regionName + GetTimeString() + ".oar");
				return path.FullName;
			case NamingType.SEQUENTIAL:
				path = new FileInfo(GetNextFile(baseDir, regionName));
				return path.FullName;
			default:
				m_log.Warn("VERY BAD: Unhandled case element " + naming.ToString());
				break;
			}
			
			return path.FullName;
		}
		
		//Welcome to the TIME STRING. 4 CORNER INTEGERS, CUBES 4 QUAD MEMORY -- No 1 Integer God.
		//(Terrible reference to <timecube.com>)
		//This format may turn out to be too unwieldy to keep...
		//Besides, that's what ctimes are for. But then how do I name each file uniquely without using a GUID?
		//Sequential numbers, right? Ugh. Almost makes TOO much sense.
		private string GetTimeString()
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
		
		//Get the next logical file name
		//I really shouldn't put fields here, but for now.... ;)
		private string m_dirName = null;
		private string m_regionName = null;
		private string GetNextFile(string dirName, string regionName)
		{
			FileInfo uniqueFile = null;
			m_dirName = dirName;
			m_regionName = regionName;
			long biggestExistingFile = HalfIntervalMaximize(1, FileExistsTest);
			biggestExistingFile++; //We don't want to overwrite the biggest existing file; we want to write to the NEXT biggest.
			
			uniqueFile = new FileInfo(m_dirName + Path.DirectorySeparatorChar + m_regionName + "_" + biggestExistingFile + ".oar");
			if(uniqueFile.Exists)
			{
				//Congratulations, your strange deletion patterns fooled my half-interval search into picking an existing file!
				//Now you get to pay the performance cost :)
				uniqueFile = UniqueFileSearchLinear(biggestExistingFile);
			}
			
			return uniqueFile.FullName;
		}
					
		private bool RunHeuristics()
		{
			return true;
		}
		
		private void ExecuteScript(string scriptName, string savePath)
		{
			//Fast path out
			if(scriptName == null || scriptName.Length <= 0)
				return;
			
			try
			{
				FileInfo fi = new FileInfo(scriptName);
				if(fi.Exists)
				{
					ProcessStartInfo psi = new ProcessStartInfo(scriptName);
					psi.Arguments = savePath;
					psi.CreateNoWindow = true;
					Process proc = Process.Start(psi);
					proc.ErrorDataReceived += HandleProcErrorDataReceived;
				}
			}
			catch(Exception e)
			{
				m_log.Warn("Exception encountered when trying to run script for oar backup " + savePath, e);
			}
		}

		void HandleProcErrorDataReceived (object sender, DataReceivedEventArgs e)
		{
			m_log.Warn("ExecuteScript hook " + ((Process)sender).ProcessName + " is yacking on stderr: " + e.Data);
		}
		
		private void StopAllTimers()
		{
			foreach(Timer t in timerMap.Keys)
			{
				t.Close();	
			}
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
		public long HalfIntervalMaximize(long start, Predicate<long> pred)
		{
			long prev = start, curr = start, biggest = 0;
			
			if(start < 0)
				throw new IndexOutOfRangeException("Start value for HalfIntervalMaximize must be non-negative");
			
			do
			{
				if(pred(curr))
				{
					if(curr > biggest)
					{
						biggest = curr;
					}
					prev = curr;
					if(curr == 0)
					{
						//Special case because 0 * 2 = 0 :)
						curr = 1;
					}
					else
					{
						//Look deeper
						curr *= 2;
					}
				}
				else
				{
					// We went too far, back off halfway
					curr = (curr + prev) / 2;
				}
			}
			while(curr - prev > 0);
			
			return biggest;
		}
		
		public bool FileExistsTest(long num)
		{
			FileInfo test = new FileInfo(m_dirName + Path.DirectorySeparatorChar + m_regionName + "_" + num + ".oar");
			return test.Exists;
		}
		
		
		//Very slow, hence why we try the HalfIntervalMaximize first!
		public FileInfo UniqueFileSearchLinear(long start)
		{
			long l = start;
			FileInfo retval = null;
			do
			{
				retval = new FileInfo(m_dirName + Path.DirectorySeparatorChar + m_regionName + "_" + (l++) + ".oar");
			}
			while(retval.Exists);
			
			return retval;
		}
}
	
}

