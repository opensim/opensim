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
using OpenSim.Framework.Console;
using OpenSim.Framework.Interfaces;
using Db4objects.Db4o;

namespace OpenGrid.Config.GridConfigDb4o
{
	public class Db40ConfigPlugin: IGridConfig
	{
		public GridConfig GetConfigObject()
		{
			OpenSim.Framework.Console.MainConsole.Instance.WriteLine("Loading Db40Config dll");
			return ( new DbGridConfig());
		}
	}
	
	public class DbGridConfig : GridConfig
	{
		private IObjectContainer db;	
		
		public void LoadDefaults() {
			OpenSim.Framework.Console.MainConsole.Instance.WriteLine("Config.cs:LoadDefaults() - Please press enter to retain default or enter new settings");
			
			this.GridOwner = OpenSim.Framework.Console.MainConsole.Instance.CmdPrompt("Grid owner [OGS development team]: ", "OGS development team");

			this.DefaultAssetServer = OpenSim.Framework.Console.MainConsole.Instance.CmdPrompt("Default asset server [no default]: ");
            		this.AssetSendKey = OpenSim.Framework.Console.MainConsole.Instance.CmdPrompt("Key to send to asset server: ");
            		this.AssetRecvKey = OpenSim.Framework.Console.MainConsole.Instance.CmdPrompt("Key to expect from asset server: ");

		        this.DefaultUserServer = OpenSim.Framework.Console.MainConsole.Instance.CmdPrompt("Default user server [no default]: ");
            		this.UserSendKey = OpenSim.Framework.Console.MainConsole.Instance.CmdPrompt("Key to send to user server: ");
            		this.UserRecvKey = OpenSim.Framework.Console.MainConsole.Instance.CmdPrompt("Key to expect from user server: ");

            		this.SimSendKey = OpenSim.Framework.Console.MainConsole.Instance.CmdPrompt("Key to send to sims: ");
            		this.SimRecvKey = OpenSim.Framework.Console.MainConsole.Instance.CmdPrompt("Key to expect from sims: ");
		}

		public override void InitConfig() {
			try {
				db = Db4oFactory.OpenFile("opengrid.yap");
				IObjectSet result = db.Get(typeof(DbGridConfig));
				if(result.Count==1) {
					OpenSim.Framework.Console.MainConsole.Instance.WriteLine("Config.cs:InitConfig() - Found a GridConfig object in the local database, loading");
					foreach (DbGridConfig cfg in result) {
						this.GridOwner=cfg.GridOwner;
						this.DefaultAssetServer=cfg.DefaultAssetServer;
						this.AssetSendKey=cfg.AssetSendKey;
						this.AssetRecvKey=cfg.AssetRecvKey;
						this.DefaultUserServer=cfg.DefaultUserServer;
						this.UserSendKey=cfg.UserSendKey;
						this.UserRecvKey=cfg.UserRecvKey;
						this.SimSendKey=cfg.SimSendKey;
						this.SimRecvKey=cfg.SimRecvKey;
					}
				} else {
					OpenSim.Framework.Console.MainConsole.Instance.WriteLine("Config.cs:InitConfig() - Could not find object in database, loading precompiled defaults");
					LoadDefaults();
					OpenSim.Framework.Console.MainConsole.Instance.WriteLine("Writing out default settings to local database");
					db.Set(this);
					db.Close();
				}
			} catch(Exception e) {
				OpenSim.Framework.Console.MainConsole.Instance.WriteLine("Config.cs:InitConfig() - Exception occured");
				OpenSim.Framework.Console.MainConsole.Instance.WriteLine(e.ToString());
			}
			
			OpenSim.Framework.Console.MainConsole.Instance.WriteLine("Grid settings loaded:");
			OpenSim.Framework.Console.MainConsole.Instance.WriteLine("Grid owner: " + this.GridOwner);
			OpenSim.Framework.Console.MainConsole.Instance.WriteLine("Default asset server: " + this.DefaultAssetServer);
			OpenSim.Framework.Console.MainConsole.Instance.WriteLine("Key to send to asset server: " + this.AssetSendKey);
			OpenSim.Framework.Console.MainConsole.Instance.WriteLine("Key to expect from asset server: " + this.AssetRecvKey);
			OpenSim.Framework.Console.MainConsole.Instance.WriteLine("Default user server: " + this.DefaultUserServer);
			OpenSim.Framework.Console.MainConsole.Instance.WriteLine("Key to send to user server: " + this.UserSendKey);
			OpenSim.Framework.Console.MainConsole.Instance.WriteLine("Key to expect from user server: " + this.UserRecvKey);
			OpenSim.Framework.Console.MainConsole.Instance.WriteLine("Key to send to sims: " + this.SimSendKey);
			OpenSim.Framework.Console.MainConsole.Instance.WriteLine("Key to expect from sims: " + this.SimRecvKey);
		}
	

		public void Shutdown() {
			db.Close();
		}
	}
	
}
