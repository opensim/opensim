/*
* Copyright (c) OpenSim project, http://sim.opensecondlife.org/
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
* 
*/
using System;
using System.Collections.Generic;
using OpenSim;
using OpenSim.world;
using Db4objects.Db4o;

namespace Db40SimConfig
{
	public class Db40ConfigPlugin: ISimConfig
	{
		public SimConfig GetConfigObject()
		{
			ServerConsole.MainConsole.Instance.WriteLine("Loading Db40Config dll");
			return ( new DbSimConfig());
		}
	}
	
	public class DbSimConfig :SimConfig
	{
		private IObjectContainer db;	
		
		public void LoadDefaults() {
			ServerConsole.MainConsole.Instance.WriteLine("Config.cs:LoadDefaults() - Please press enter to retain default or enter new settings");
			
			this.RegionName=ServerConsole.MainConsole.Instance.CmdPrompt("Name [OpenSim test]: ","OpenSim test");
			this.RegionLocX=(uint)Convert.ToInt32(ServerConsole.MainConsole.Instance.CmdPrompt("Grid Location X [997]: ","997"));
			this.RegionLocY=(uint)Convert.ToInt32(ServerConsole.MainConsole.Instance.CmdPrompt("Grid Location Y [996]: ","996"));
			this.IPListenPort=Convert.ToInt32(ServerConsole.MainConsole.Instance.CmdPrompt("UDP port for client connections [9000]: ","9000"));
			this.IPListenAddr=ServerConsole.MainConsole.Instance.CmdPrompt("IP Address to listen on for client connections [127.0.0.1]: ","127.0.0.1");
			
			if(!OpenSim_Main.sim.sandbox)
			{
				this.AssetURL=ServerConsole.MainConsole.Instance.CmdPrompt("Asset server URL: ");
				this.AssetSendKey=ServerConsole.MainConsole.Instance.CmdPrompt("Asset server key: ");
				this.GridURL=ServerConsole.MainConsole.Instance.CmdPrompt("Grid server URL: ");
				this.GridSendKey=ServerConsole.MainConsole.Instance.CmdPrompt("Grid server key: ");
			}
			this.RegionHandle = Util.UIntsToLong((RegionLocX*256), (RegionLocY*256));
		}

		public override void InitConfig() {
			try {
				db = Db4oFactory.OpenFile("opensim.yap");
				IObjectSet result = db.Get(typeof(DbSimConfig));
				if(result.Count==1) {
					ServerConsole.MainConsole.Instance.WriteLine("Config.cs:InitConfig() - Found a SimConfig object in the local database, loading");
					foreach (DbSimConfig cfg in result) {
						this.RegionName = cfg.RegionName;
						this.RegionLocX = cfg.RegionLocX;
						this.RegionLocY = cfg.RegionLocY;
						this.RegionHandle = Util.UIntsToLong((RegionLocX*256), (RegionLocY*256));
						this.IPListenPort = cfg.IPListenPort;
						this.IPListenAddr = cfg.IPListenAddr;
						this.AssetURL = cfg.AssetURL;
						this.AssetSendKey = cfg.AssetSendKey;
						this.GridURL = cfg.GridURL;
						this.GridSendKey = cfg.GridSendKey;
					}
				} else {
					ServerConsole.MainConsole.Instance.WriteLine("Config.cs:InitConfig() - Could not find object in database, loading precompiled defaults");
					LoadDefaults();
					ServerConsole.MainConsole.Instance.WriteLine("Writing out default settings to local database");
					db.Set(this);
				}
			} catch(Exception e) {
				db.Close();
				ServerConsole.MainConsole.Instance.WriteLine("Config.cs:InitConfig() - Exception occured");
				ServerConsole.MainConsole.Instance.WriteLine(e.ToString());
			}
			
			ServerConsole.MainConsole.Instance.WriteLine("Sim settings loaded:");
			ServerConsole.MainConsole.Instance.WriteLine("Name: " + this.RegionName);
			ServerConsole.MainConsole.Instance.WriteLine("Region Location: [" + this.RegionLocX.ToString() + "," + this.RegionLocY + "]");
			ServerConsole.MainConsole.Instance.WriteLine("Region Handle: " + this.RegionHandle.ToString());
			ServerConsole.MainConsole.Instance.WriteLine("Listening on IP: " + this.IPListenAddr + ":" + this.IPListenPort);
			ServerConsole.MainConsole.Instance.WriteLine("Sandbox Mode? " + OpenSim_Main.sim.sandbox.ToString());
			ServerConsole.MainConsole.Instance.WriteLine("Asset URL: " + this.AssetURL);
			ServerConsole.MainConsole.Instance.WriteLine("Asset key: " + this.AssetSendKey);
			ServerConsole.MainConsole.Instance.WriteLine("Grid URL: " + this.GridURL);
			ServerConsole.MainConsole.Instance.WriteLine("Grid key: " + this.GridSendKey);
		}
	
		public override World LoadWorld() 
		{
			ServerConsole.MainConsole.Instance.WriteLine("Config.cs:LoadWorld() - Loading world....");
			World blank = new World();
			ServerConsole.MainConsole.Instance.WriteLine("Config.cs:LoadWorld() - Looking for a heightmap in local DB");
			IObjectSet world_result = db.Get(new float[65536]);
			if(world_result.Count>0) {
				ServerConsole.MainConsole.Instance.WriteLine("Config.cs:LoadWorld() - Found a heightmap in local database, loading");
				blank.LandMap=(float[])world_result.Next();	
			} else {
				ServerConsole.MainConsole.Instance.WriteLine("Config.cs:LoadWorld() - No heightmap found, generating new one");
				HeightmapGenHills hills = new HeightmapGenHills();
                blank.LandMap = hills.GenerateHeightmap(200, 4.0f, 80.0f, false);
				ServerConsole.MainConsole.Instance.WriteLine("Config.cs:LoadWorld() - Saving heightmap to local database");
				db.Set(blank.LandMap);
				db.Commit();
			}
			return blank;
		}

		public override void LoadFromGrid() {
			ServerConsole.MainConsole.Instance.WriteLine("Config.cs:LoadFromGrid() - dummy function, DOING ABSOLUTELY NOTHING AT ALL!!!");
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
