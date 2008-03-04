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
* 
*/
using System;
using System.Runtime.Remoting;
using System.Threading;
using Mono.Addins;
using OpenSim;
using OpenSim.Framework.Console;
using MonitorLib;

[assembly:Addin]
[assembly:AddinDependency ("OpenSim", "0.5")]

namespace OpenSim.ApplicationPlugins.RegionMonitor
{
	[Extension("/OpenSim/Startup")]
	public class RegionMonitorPlugin : MonitorLibBase, IApplicationPlugin
	{
		protected Thread m_mointorThread;
		protected static OpenSimMain m_openSimMain;

		public void Initialise(OpenSimMain opensim)
		{
			m_openSimMain = opensim;
			Start();
			MainLog.Instance.Verbose("Monitor", "Region monitor is runing ...");
		}

		public void Close()
		{
		}

		public void Start()
		{
			// start monitor thread (remoting module)
			m_mointorThread = new Thread(new ThreadStart(StartMonitor));
			m_mointorThread.IsBackground = true;
			m_mointorThread.Start();
		}

		private void StartMonitor()
		{
			try
			{
				Object lockObj = new Object();

				RemotingConfiguration.Configure("monitorS.config", false);

				lock (lockObj)
				{
					System.Threading.Monitor.Wait(lockObj);
				}
			}
			catch (Exception e)
			{
				MainLog.Instance.Warn("MONITOR", "Error - " + e.Message);
			}
		}

		public override bool FetchInfo(out string outstr)
		{
			MainLog.Instance.Verbose("MONITOR", "Fetch Information from Region server");
			bool status = true;
			string startTime = "";
			string upTime = "";
			int userNumber = 0;
			int regionNumber = 0;
			m_openSimMain.GetRunTime(out startTime, out upTime);
			m_openSimMain.GetAvatarNumber(out userNumber);
			m_openSimMain.GetRegionNumber(out regionNumber);
			outstr = startTime
					+ "," + upTime
					+ "," + regionNumber
					+ "," + userNumber;
			return status;
		}


		public override bool MoveRegion()
		{
			MainLog.Instance.Verbose("MONITOR", "Move Region");
			bool status = true;

			return status;
		}

		public override bool SplitRegion()
		{
			MainLog.Instance.Verbose("MONITOR", "Split Region");
			bool status = true;

			return status;
		}

		public override bool MergeScenes()
		{
			MainLog.Instance.Verbose("MONITOR", "Merge Scenes");
			bool status = true;

			return status;
		}

	}
}
