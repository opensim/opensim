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
		
        	public string AssetURL;
	        public string AssetSendKey;
		
	        public string GridURL;
	        public string GridSendKey;

		private IObjectContainer db;
		
		public void LoadDefaults() {
			this.RegionName = "OpenSim test\0";
			this.RegionLocX = 997;
			this.RegionLocY = 996;
			this.RegionHandle = Helpers.UIntsToLong((RegionLocX*256), (RegionLocY*256));
			this.IPListenPort = 9000;
			this.IPListenAddr = "4.78.190.75";
			this.AssetURL = "http://osgrid.org/ogs/assetserver/";
			this.AssetSendKey = "1234";
			this.GridURL = "http://osgrid.org/ogs/gridserver/";
			this.GridSendKey = "1234";
		}

		public void InitConfig() {
			try {
				db = Db4oFactory.OpenFile("opensim.yap");
				IObjectSet result = db.Get(typeof(SimConfig));
				if(result.Count==1) {
					Console.WriteLine("Config.cs:InitConfig() - Found a SimConfig object in the local database, loading");
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
					Console.WriteLine("Config.cs:InitConfig() - Could not find object in database, loading precompiled defaults");
					LoadDefaults();
					Console.WriteLine("Writing out default settings to local database");
					db.Set(this);
				}
			} catch(Exception e) {
				db.Close();
				Console.WriteLine("Config.cs:InitConfig() - Exception occured");
				Console.WriteLine(e.ToString());
			}
		}
	
		public World LoadWorld() {
			IObjectSet world_result = db.Get(typeof(OpenSim.world.World));
			if(world_result.Count==1) {
				Console.WriteLine("Config.cs:LoadWorld() - Found an OpenSim.world.World object in local database, loading");
				return (World)world_result.Next();	
			} else {
				Console.WriteLine("Config.cs:LoadWorld() - Could not find the world or too many worlds! Constructing blank one");
				World blank = new World();
				Console.WriteLine("Config.cs:LoadWorld() - Saving initial world state to disk");
				db.Set(blank);
				db.Commit();
				return blank;	
			}
		}

		public void LoadFromGrid() {
			Console.WriteLine("Config.cs:LoadFromGrid() - dummy function, DOING ABSOLUTELY NOTHING AT ALL!!!");
			// TODO: Make this crap work
			/* WebRequest GridLogin = WebRequest.Create(this.GridURL + "regions/" + this.RegionHandle.ToString() + "/login");
			WebResponse GridResponse = GridLogin.GetResponse();
	                byte[] idata = new byte[(int)GridResponse.ContentLength];
			BinaryReader br = new BinaryReader(GridResponse.GetResponseStream());
			
			br.Close();
			GridResponse.Close();
		 	*/										    
		}

		public void Shutdown() {
			db.Close();
		}
	}
}
