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
			Console.WriteLine("Config.cs:LoadDefaults() - Please press enter to retain default or enter new settings");
			Console.Write("Name [OpenSim test]:");
			tempstring=Console.ReadLine();
			if(tempstring=="") {
				this.RegionName = "OpenSim test";
			} else {
				this.RegionName = tempstring;
			}

			Console.Write("Grid location X [997]:");
			tempstring=Console.ReadLine();
			if(tempstring=="") {
				this.RegionLocX = 997;
			} else {
				this.RegionLocX = (uint)Convert.ToInt32(tempstring);
			}
		
			Console.Write("Grid location Y [996]:");
			tempstring=Console.ReadLine();
			if(tempstring=="") {
				this.RegionLocY = 996;
			} else {
				this.RegionLocY = (uint)Convert.ToInt32(tempstring);
			}

			Console.Write("Listen on UDP port for client connections [9000]:");
			tempstring=Console.ReadLine();
                        if(tempstring=="") {
                                this.IPListenPort = 9000;
                        } else {
                                this.IPListenPort = Convert.ToInt32(tempstring);
                        }

			Console.Write("Listen on IP address for client connections [127.0.0.1]:");
			tempstring=Console.ReadLine();
			if(tempstring=="") {
                                this.IPListenAddr = "127.0.0.1";
                        } else {
                                this.IPListenAddr = tempstring;
                        }
	
	
			Console.Write("Run in sandbox or grid mode? [sandbox]:");
			tempstring=Console.ReadLine();
			if(tempstring=="") {
                                this.sandbox = true;
                        } else if(tempstring=="grid"){
                                this.sandbox = false;
                        } else if(tempstring=="sandbox"){
				this.sandbox=true;
			}

			if(!this.sandbox) {
				Console.Write("Asset server URL:");
				this.AssetURL=Console.ReadLine();
				Console.Write("Key to send to asset server:");
				this.AssetSendKey=Console.ReadLine();
				Console.Write("Grid server URL:");
				this.GridURL=Console.ReadLine();
				Console.Write("Key to send to gridserver:");
				this.GridSendKey=Console.ReadLine();
				
			}
			this.RegionHandle = Helpers.UIntsToLong((RegionLocX*256), (RegionLocY*256));
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
			Console.WriteLine("Sim settings loaded:");
			Console.WriteLine("Name: " + this.RegionName);
			Console.WriteLine("Region Location: [" + this.RegionLocX.ToString() + "," + this.RegionLocY + "]");
			Console.WriteLine("Region Handle: " + this.RegionHandle.ToString());
			Console.WriteLine("Listening on IP: " + this.IPListenAddr + ":" + this.IPListenPort);
			Console.WriteLine("Sandbox Mode? " + this.sandbox.ToString());
			Console.WriteLine("Asset URL: " + this.AssetURL);
			Console.WriteLine("Asset key: " + this.AssetSendKey);
			Console.WriteLine("Grid URL: " + this.GridURL);
			Console.WriteLine("Grid key: " + this.GridSendKey);
		}
	
		public World LoadWorld() {
			Console.WriteLine("Config.cs:LoadWorld() - Looking for a world object in local DB");
	//		IObjectSet world_result = db.Get(typeof(OpenSim.world.World));
	//		if(world_result.Count==1) {
	//			Console.WriteLine("Config.cs:LoadWorld() - Found an OpenSim.world.World object in local database, loading");
				//return (World)world_result.Next();	
	//		} else {
				Console.WriteLine("Config.cs:LoadWorld() - Could not find the world or too many worlds! Constructing blank one");
				World blank = new World();
				Console.WriteLine("Config.cs:LoadWorld() - Saving initial world state to disk");
				//db.Set(blank);
				//db.Commit();
				return blank;	
	//		}
		}

		public void LoadFromGrid() {
			Console.WriteLine("Config.cs:LoadFromGrid() - dummy function, DOING ABSOLUTELY NOTHING AT ALL!!!");
			// TODO: Make this crap work
		}

		public void Shutdown() {
			db.Close();
		}
	}
}
