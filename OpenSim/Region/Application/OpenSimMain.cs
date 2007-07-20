/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
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
using System.IO;
using libsecondlife;
using OpenSim.Assets;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Console;
using OpenSim.Framework.Data;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Types;
using OpenSim.Framework.Configuration;
using OpenSim.Physics.Manager;
using OpenSim.Region.Caches;
using OpenSim.Region.ClientStack;
using OpenSim.Region.Communications.Local;
using OpenSim.Region.Communications.OGS1;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Environment;
using System.Text;
using System.Collections.Generic;

namespace OpenSim
{

    public class OpenSimMain : RegionApplicationBase, conscmd_callback
    {
        public string m_physicsEngine;
        public bool m_sandbox;
        public bool m_loginserver;
        public bool user_accounts;
        public bool m_gridLocalAsset;
        protected bool m_useConfigFile;
        public string m_configFileName;

        protected List<UDPServer> m_udpServers = new List<UDPServer>();
        protected List<RegionInfo> m_regionData = new List<RegionInfo>();
        protected List<IWorld> m_localWorld = new List<IWorld>();

        private bool m_silent;
        private string m_logFilename = "region-console-" + Guid.NewGuid().ToString() + ".log";

        public OpenSimMain(bool sandBoxMode, bool startLoginServer, string physicsEngineName, bool useConfigFile, bool silent, string configFileName)
        :base( )
        {
            m_useConfigFile = useConfigFile;
            m_sandbox = sandBoxMode;
            m_loginserver = startLoginServer;
            m_physicsEngine = physicsEngineName;
            m_configFileName = configFileName;
            m_silent = silent;
        }

        
        /// <summary>
        /// Performs initialisation of the world, such as loading configuration from disk.
        /// </summary>
        public override void StartUp()
        {
            m_log = new LogBase(m_logFilename, "Region", this, m_silent);
            MainLog.Instance = m_log;

            base.StartUp();

            if (!m_sandbox)
            {
                m_httpServer.AddStreamHandler(new SimStatusHandler());
            }

            if (m_sandbox)
            {
                m_commsManager = new CommunicationsLocal( m_networkServersInfo, m_httpServer);
            }
            else
            {
                m_commsManager = new CommunicationsOGS1( m_networkServersInfo, m_httpServer );
            }

            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Regions");
            string[] configFiles = Directory.GetFiles(path, "*.xml");

            if (configFiles.Length == 0)
            {
                string path2 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Regions");
                string path3 = Path.Combine(path2, "default.xml");

                RegionInfo regionInfo = new RegionInfo("DEFAULT REGION CONFIG", path3);
                configFiles = Directory.GetFiles(path, "*.xml");
            }

            for (int i = 0; i < configFiles.Length; i++)
            {
                Console.WriteLine("Loading region config file");
                RegionInfo regionInfo = new RegionInfo("REGION CONFIG #" + (i + 1), configFiles[i]);
                
                UDPServer udpServer;
                Scene scene = SetupScene(regionInfo, out udpServer);
                
                m_localWorld.Add(scene);
                
                
                m_udpServers.Add(udpServer);
                m_regionData.Add(regionInfo);
            }

            // Start UDP servers
            for (int i = 0; i < m_udpServers.Count; i++)
            {
                this.m_udpServers[i].ServerListener();
            }

        }

        protected override StorageManager CreateStorageManager(RegionInfo regionInfo)
        {
            return new StorageManager("OpenSim.DataStore.NullStorage.dll", regionInfo.DataStore, regionInfo.RegionName);
        }

        protected override Scene CreateScene(RegionInfo regionInfo, StorageManager storageManager, AgentCircuitManager circuitManager)
        {
            return new Scene(regionInfo, circuitManager, m_commsManager, m_assetCache, storageManager, m_httpServer);
        }
        
        protected override void Initialize()
        {
            m_networkServersInfo = new NetworkServersInfo("NETWORK SERVERS INFO", "network_servers_information.xml");
            m_httpServerPort = m_networkServersInfo.HttpListenerPort;
            m_assetCache = new AssetCache("OpenSim.Region.GridInterfaces.Local.dll", m_networkServersInfo.AssetURL, m_networkServersInfo.AssetSendKey);
        }

        protected override LogBase CreateLog()
        {
            return new LogBase(m_logFilename, "Region", this, m_silent);
        }

        # region Setup methods

        protected override PhysicsScene GetPhysicsScene( )
        {
            return GetPhysicsScene( m_physicsEngine );
        }

        private class SimStatusHandler : IStreamHandler
        {
            public byte[] Handle(string path, Stream request)
            {
                return Encoding.UTF8.GetBytes("OK");
            }

            public string ContentType
            {
                get { return "text/plain"; }
            }

            public string HttpMethod
            {
                get { return "GET"; }
            }
            
            public string Path
            {
                get { return "/simstatus/"; }
            }
        }

        protected void ConnectToRemoteGridServer()
        {

        }

        #endregion

        /*private void SetupFromConfigFile(IGenericConfig configData)
        {
            // Log filename
            string attri = "";
            attri = configData.GetAttribute("LogFilename");
            if (String.IsNullOrEmpty(attri))
            {
            }
            else
            {
                m_logFilename = attri;
            }

            // SandBoxMode
            attri = "";
            attri = configData.GetAttribute("SandBox");
            if ((attri == "") || ((attri != "false") && (attri != "true")))
            {
                this.m_sandbox = false;
                configData.SetAttribute("SandBox", "false");
            }
            else
            {
                this.m_sandbox = Convert.ToBoolean(attri);
            }

            // LoginServer
            attri = "";
            attri = configData.GetAttribute("LoginServer");
            if ((attri == "") || ((attri != "false") && (attri != "true")))
            {
                this.m_loginserver = false;
                configData.SetAttribute("LoginServer", "false");
            }
            else
            {
                this.m_loginserver = Convert.ToBoolean(attri);
            }

            // Sandbox User accounts
            attri = "";
            attri = configData.GetAttribute("UserAccount");
            if ((attri == "") || ((attri != "false") && (attri != "true")))
            {
                this.user_accounts = false;
                configData.SetAttribute("UserAccounts", "false");
            }
            else if (attri == "true")
            {
                this.user_accounts = Convert.ToBoolean(attri);
            }

            // Grid mode hack to use local asset server
            attri = "";
            attri = configData.GetAttribute("LocalAssets");
            if ((attri == "") || ((attri != "false") && (attri != "true")))
            {
                this.m_gridLocalAsset = false;
                configData.SetAttribute("LocalAssets", "false");
            }
            else if (attri == "true")
            {
                this.m_gridLocalAsset = Convert.ToBoolean(attri);
            }

            
            attri = "";
            attri = configData.GetAttribute("PhysicsEngine");
            switch (attri)
            {
                default:
                    throw new ArgumentException(String.Format( "Invalid value [{0}] for PhysicsEngine attribute, terminating", attri ) );

                case "":
                case "basicphysics":
                    this.m_physicsEngine = "basicphysics";
                    configData.SetAttribute("PhysicsEngine", "basicphysics");
                    ScenePresence.PhysicsEngineFlying = false;
                    break;

                case "RealPhysX":
                    this.m_physicsEngine = "RealPhysX";
                    ScenePresence.PhysicsEngineFlying = true;
                    break;

                case "OpenDynamicsEngine":
                    this.m_physicsEngine = "OpenDynamicsEngine";
                    ScenePresence.PhysicsEngineFlying = true;
                    break;

                case "BulletXEngine":
                    this.m_physicsEngine = "BulletXEngine";
                    ScenePresence.PhysicsEngineFlying = true;
                    break;
            }

            configData.Commit();

        }*/

        /// <summary>
        /// Performs any last-minute sanity checking and shuts down the region server
        /// </summary>
        public virtual void Shutdown()
        {
            m_log.Verbose("Main.cs:Shutdown() - Closing all threads");
            m_log.Verbose("Main.cs:Shutdown() - Killing listener thread");
            m_log.Verbose("Main.cs:Shutdown() - Killing clients");
            // IMPLEMENT THIS
            m_log.Verbose("Main.cs:Shutdown() - Closing console and terminating");
            for (int i = 0; i < m_localWorld.Count; i++)
            {
                ((Scene)m_localWorld[i]).Close();
            }
            m_log.Close();
            Environment.Exit(0);
        }

        #region Console Commands
        /// <summary>
        /// Runs commands issued by the server console from the operator
        /// </summary>
        /// <param name="command">The first argument of the parameter (the command)</param>
        /// <param name="cmdparams">Additional arguments passed to the command</param>
        public void RunCmd(string command, string[] cmdparams)
        {
            switch (command)
            {
                case "help":
                    m_log.Error("show users - show info about connected users");
                    m_log.Error("quit - disconnect all clients and shutdown");
                    break;

                case "show":
                    if (cmdparams.Length > 0)
                    {
                        Show(cmdparams[0]);
                    }
                    break;

                case "terrain":
                    string result = "";
                    for (int i = 0; i < m_localWorld.Count; i++)
                    {
                        if (!((Scene)m_localWorld[i]).Terrain.RunTerrainCmd(cmdparams, ref result, m_localWorld[i].RegionInfo.RegionName))
                        {
                            m_log.Error(result);
                        }
                    }
                    break;
                case "script":
                    for (int i = 0; i < m_localWorld.Count; i++)
                    {
                        ((Scene)m_localWorld[i]).SendCommandToScripts(cmdparams);
                    }
                    break;

                case "backup":
                    for (int i = 0; i < m_localWorld.Count; i++)
                    {
                        ((Scene)m_localWorld[i]).Backup();
                    }
                    break;

                case "alert":
                    for (int i = 0; i < m_localWorld.Count; i++)
                    {
                        ((Scene)m_localWorld[i]).HandleAlertCommand(cmdparams);
                    }
                    break;

                case "quit":
                case "shutdown":
                    Shutdown();
                    break;

                default:
                    m_log.Error("Unknown command");
                    break;
            }
        }

        /// <summary>
        /// Outputs to the console information about the region
        /// </summary>
        /// <param name="ShowWhat">What information to display (valid arguments are "uptime", "users")</param>
        public void Show(string ShowWhat)
        {
            switch (ShowWhat)
            {
                case "uptime":
                    m_log.Error("OpenSim has been running since " + m_startuptime.ToString());
                    m_log.Error("That is " + (DateTime.Now - m_startuptime).ToString());
                    break;
                case "users":
                    ScenePresence TempAv;
                    m_log.Error(String.Format("{0,-16}{1,-16}{2,-25}{3,-25}{4,-16}{5,-16}{6,-16}", "Firstname", "Lastname", "Agent ID", "Session ID", "Circuit", "IP", "World"));
                    for (int i = 0; i < m_localWorld.Count; i++)
                    {
                        foreach (libsecondlife.LLUUID UUID in ((Scene)m_localWorld[i]).Entities.Keys)
                        {
                            if (((Scene)m_localWorld[i]).Entities[UUID].ToString() == "OpenSim.world.Avatar")
                            {
                                TempAv = (ScenePresence)((Scene)m_localWorld[i]).Entities[UUID];
                                m_log.Error(String.Format("{0,-16}{1,-16}{2,-25}{3,-25}{4,-16},{5,-16}{6,-16}", TempAv.firstname, TempAv.lastname, UUID, TempAv.ControllingClient.AgentId, "Unknown", "Unknown"), ((Scene)m_localWorld[i]).RegionInfo.RegionName);
                            }
                        }
                    }
                    break;
            }
        }
        #endregion
    }


}
