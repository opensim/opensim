/*
Copyright (c) OpenSim project, http://osgrid.org/

* Copyright (c) <year>, <copyright holder>
* All rights reserved.
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the <organization> nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY <copyright holder> ``AS IS'' AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL <copyright holder> BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using System;
using System.Collections.Generic;
using System.IO;
using Db4objects.Db4o;
using libsecondlife;
using OpenSim.world;

namespace OpenSim
{
	/// <summary>
	/// This class handles connection to the underlying database used for configuration of the region.
	/// Region content is also stored by this class. The main entry point is InitConfig() which attempts to locate
	/// opensim.yap in the current working directory. If opensim.yap can not be found, default settings are loaded from
	/// what is hardcoded here and then saved into opensim.yap for future startups.
	/// </summary>
	public class SimConfig 
	{
		public string RegionName;
		
		public uint RegionLocX;
		public uint RegionLocY;
		public ulong RegionHandle;
		
		public int IPListenPort;
		public string IPListenAddr;
		
		public bool sandbox;
        	public string AssetURL="";
	        public string AssetSendKey="";
		
	        public string GridURL="";
	        public string GridSendKey="";

		private IObjectContainer db;
		
		public void LoadDefaults() {
			string tempstring;
			OpenSim_Main.localcons.WriteLine("Config.cs:LoadDefaults() - Please press enter to retain default or enter new settings");
			
			this.RegionName=OpenSim_Main.localcons.CmdPrompt("Name [OpenSim test]: ","OpenSim test");
			this.RegionLocX=(uint)Convert.ToInt32(OpenSim_Main.localcons.CmdPrompt("Grid Location X [997]: ","997"));
			this.RegionLocY=(uint)Convert.ToInt32(OpenSim_Main.localcons.CmdPrompt("Grid Location Y [996]: ","996"));
			this.IPListenPort=Convert.ToInt32(OpenSim_Main.localcons.CmdPrompt("UDP port for client connections [9000]: ","9000"));
			this.IPListenAddr=OpenSim_Main.localcons.CmdPrompt("IP Address to listen on for client connections [127.0.0.1]: ","127.0.0.1");
	
	
			tempstring=OpenSim_Main.localcons.CmdPrompt("Run in sandbox or grid mode? [sandbox]: ","sandbox", "sandbox", "grid");
                        if(tempstring=="grid"){
                                this.sandbox = false;
                        } else if(tempstring=="sandbox"){
				this.sandbox=true;
			}

			if(!this.sandbox) {
				this.AssetURL=OpenSim_Main.localcons.CmdPrompt("Asset server URL: ");
				this.AssetSendKey=OpenSim_Main.localcons.CmdPrompt("Asset server key: ");
				this.GridURL=OpenSim_Main.localcons.CmdPrompt("Grid server URL: ");
				this.GridSendKey=OpenSim_Main.localcons.CmdPrompt("Grid server key: ");
			}
			this.RegionHandle = Helpers.UIntsToLong((RegionLocX*256), (RegionLocY*256));
		}

		public void InitConfig() {
			try {
				db = Db4oFactory.OpenFile("opensim.yap");
				IObjectSet result = db.Get(typeof(SimConfig));
				if(result.Count==1) {
					OpenSim_Main.localcons.WriteLine("Config.cs:InitConfig() - Found a SimConfig object in the local database, loading");
					foreach (SimConfig cfg in result) {
						this.RegionName = cfg.RegionName;
						this.RegionLocX = cfg.RegionLocX;
						this.RegionLocY = cfg.RegionLocY;
						this.RegionHandle = Helpers.UIntsToLong((RegionLocX*256), (RegionLocY*256));
						this.IPListenPort = cfg.IPListenPort;
						this.IPListenAddr = cfg.IPListenAddr;
						this.AssetURL = cfg.AssetURL;
						this.AssetSendKey = cfg.AssetSendKey;
						this.GridURL = cfg.GridURL;
						this.GridSendKey = cfg.GridSendKey;
					}
				} else {
					OpenSim_Main.localcons.WriteLine("Config.cs:InitConfig() - Could not find object in database, loading precompiled defaults");
					LoadDefaults();
					OpenSim_Main.localcons.WriteLine("Writing out default settings to local database");
					db.Set(this);
				}
			} catch(Exception e) {
				db.Close();
				OpenSim_Main.localcons.WriteLine("Config.cs:InitConfig() - Exception occured");
				OpenSim_Main.localcons.WriteLine(e.ToString());
			}
			OpenSim_Main.localcons.WriteLine("Sim settings loaded:");
			OpenSim_Main.localcons.WriteLine("Name: " + this.RegionName);
			OpenSim_Main.localcons.WriteLine("Region Location: [" + this.RegionLocX.ToString() + "," + this.RegionLocY + "]");
			OpenSim_Main.localcons.WriteLine("Region Handle: " + this.RegionHandle.ToString());
			OpenSim_Main.localcons.WriteLine("Listening on IP: " + this.IPListenAddr + ":" + this.IPListenPort);
			OpenSim_Main.localcons.WriteLine("Sandbox Mode? " + this.sandbox.ToString());
			OpenSim_Main.localcons.WriteLine("Asset URL: " + this.AssetURL);
			OpenSim_Main.localcons.WriteLine("Asset key: " + this.AssetSendKey);
			OpenSim_Main.localcons.WriteLine("Grid URL: " + this.GridURL);
			OpenSim_Main.localcons.WriteLine("Grid key: " + this.GridSendKey);
		}
	
		public World LoadWorld() {
			OpenSim_Main.localcons.WriteLine("Config.cs:LoadWorld() - Loading world....");
			World blank = new World();
			OpenSim_Main.localcons.WriteLine("Config.cs:LoadWorld() - Looking for a heightmap in local DB");
			IObjectSet world_result = db.Get(new float[65536]);
			if(world_result.Count>0) {
				OpenSim_Main.localcons.WriteLine("Config.cs:LoadWorld() - Found a heightmap in local database, loading");
				blank.LandMap=(float[])world_result.Next();	
			} else {
				OpenSim_Main.localcons.WriteLine("Config.cs:LoadWorld() - No heightmap found, generating new one");
				for(int i =0; i < 65536; i++) {
                        		blank.LandMap[i] =  21.4989f;
                		}
				OpenSim_Main.localcons.WriteLine("Config.cs:LoadWorld() - Saving heightmap to local database");
				db.Set(blank.LandMap);
				db.Commit();
			}
			return blank;
		}

		public void LoadFromGrid() {
			OpenSim_Main.localcons.WriteLine("Config.cs:LoadFromGrid() - dummy function, DOING ABSOLUTELY NOTHING AT ALL!!!");
			// TODO: Make this crap work
		}

		public void Shutdown() {
			db.Close();
		}
	}
}
