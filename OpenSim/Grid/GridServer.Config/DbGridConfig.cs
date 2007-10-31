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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
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
using Db4objects.Db4o;
using OpenSim.Framework.Configuration;
using OpenSim.Framework.Console;
using OpenSim.Framework.Interfaces;

namespace OpenGrid.Config.GridConfigDb4o
{
    /// <summary>
    /// A grid configuration interface for returning the DB4o Config Provider
    /// </summary>
	public class Db40ConfigPlugin: IGridConfig
	{
        /// <summary>
        /// Loads and returns a configuration objeect
        /// </summary>
        /// <returns>A grid configuration object</returns>
		public GridConfig GetConfigObject()
		{
			MainLog.Instance.Verbose("Loading Db40Config dll");
			return ( new DbGridConfig());
		}
	}
	
    /// <summary>
    /// A DB4o based Gridserver configuration object
    /// </summary>
	public class DbGridConfig : GridConfig
	{
        /// <summary>
        /// The DB4o Database
        /// </summary>
		private IObjectContainer db;	
		
        /// <summary>
        /// User configuration for the Grid Config interfaces
        /// </summary>
		public void LoadDefaults() {
			MainLog.Instance.Notice("DbGridConfig.cs:LoadDefaults() - Please press enter to retain default or enter new settings");
			
            // About the grid options
			this.GridOwner = MainLog.Instance.CmdPrompt("Grid owner", "OGS development team");

            // Asset Options
			this.DefaultAssetServer = MainLog.Instance.CmdPrompt("Default asset server","http://127.0.0.1:" + AssetConfig.DefaultHttpPort.ToString() + "/");
            this.AssetSendKey = MainLog.Instance.CmdPrompt("Key to send to asset server","null");
            this.AssetRecvKey = MainLog.Instance.CmdPrompt("Key to expect from asset server","null");

            // User Server Options
	        this.DefaultUserServer = MainLog.Instance.CmdPrompt("Default user server","http://127.0.0.1:" + UserConfig.DefaultHttpPort.ToString() + "/");
        	this.UserSendKey = MainLog.Instance.CmdPrompt("Key to send to user server","null");
        	this.UserRecvKey = MainLog.Instance.CmdPrompt("Key to expect from user server","null");

            // Region Server Options
            this.SimSendKey = MainLog.Instance.CmdPrompt("Key to send to sims","null");
            this.SimRecvKey = MainLog.Instance.CmdPrompt("Key to expect from sims","null");
		}

        /// <summary>
        /// Initialises a new configuration object
        /// </summary>
		public override void InitConfig() {
			try {
                // Perform Db4o initialisation
				db = Db4oFactory.OpenFile("opengrid.yap");

                // Locate the grid configuration object
				IObjectSet result = db.Get(typeof(DbGridConfig));
                // Found?
				if(result.Count==1) {
					MainLog.Instance.Verbose("DbGridConfig.cs:InitConfig() - Found a GridConfig object in the local database, loading");
					foreach (DbGridConfig cfg in result) {
                        // Import each setting into this class
                        // Grid Settings
						this.GridOwner=cfg.GridOwner;
                        // Asset Settings
						this.DefaultAssetServer=cfg.DefaultAssetServer;
						this.AssetSendKey=cfg.AssetSendKey;
						this.AssetRecvKey=cfg.AssetRecvKey;
                        // User Settings
						this.DefaultUserServer=cfg.DefaultUserServer;
						this.UserSendKey=cfg.UserSendKey;
						this.UserRecvKey=cfg.UserRecvKey;
                        // Region Settings
						this.SimSendKey=cfg.SimSendKey;
						this.SimRecvKey=cfg.SimRecvKey;
					}
                // Create a new configuration object from this class
				} else {
					MainLog.Instance.Verbose("DbGridConfig.cs:InitConfig() - Could not find object in database, loading precompiled defaults");

                    // Load default settings into this class
					LoadDefaults();

                    // Saves to the database file...
                    MainLog.Instance.Verbose( "Writing out default settings to local database");
					db.Set(this);

                    // Closes file locks
					db.Close();
				}
			} catch(Exception e) {
				MainLog.Instance.Warn("DbGridConfig.cs:InitConfig() - Exception occured");
                MainLog.Instance.Warn(e.ToString());
			}
			
            // Grid Settings
			MainLog.Instance.Verbose("Grid settings loaded:");
			MainLog.Instance.Verbose("Grid owner: " + this.GridOwner);

            // Asset Settings
			MainLog.Instance.Verbose("Default asset server: " + this.DefaultAssetServer);
			MainLog.Instance.Verbose("Key to send to asset server: " + this.AssetSendKey);
			MainLog.Instance.Verbose("Key to expect from asset server: " + this.AssetRecvKey);

            // User Settings
			MainLog.Instance.Verbose("Default user server: " + this.DefaultUserServer);
			MainLog.Instance.Verbose("Key to send to user server: " + this.UserSendKey);
			MainLog.Instance.Verbose("Key to expect from user server: " + this.UserRecvKey);

            // Region Settings
			MainLog.Instance.Verbose("Key to send to sims: " + this.SimSendKey);
			MainLog.Instance.Verbose("Key to expect from sims: " + this.SimRecvKey);
		}
	
        /// <summary>
        /// Closes down the database and releases filesystem locks
        /// </summary>
		public void Shutdown() {
			db.Close();
		}
	}
	
}
