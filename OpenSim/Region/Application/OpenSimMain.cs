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
using Nini.Config;
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

using OpenSim.Region.ClientStack;
using OpenSim.Region.Communications.Local;
using OpenSim.Region.Communications.OGS1;
using OpenSim.Framework.Communications.Caches;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Environment;
using System.Text;
using System.Collections.Generic;
using OpenSim.Framework.Utilities;

namespace OpenSim
{

    public class OpenSimMain : RegionApplicationBase, conscmd_callback
    {
        public string m_physicsEngine;
        public bool m_sandbox;
        public bool user_accounts;
        public bool m_gridLocalAsset;
        protected bool m_useConfigFile;
        public string m_configFileName;

        protected List<UDPServer> m_udpServers = new List<UDPServer>();
        protected List<RegionInfo> m_regionData = new List<RegionInfo>();
        protected List<IScene> m_localScenes = new List<IScene>();

        private bool m_silent;
        private string m_logFilename = ("region-console-" + Guid.NewGuid().ToString() + ".log");

        public OpenSimMain(IConfigSource configSource)
            : base()
        {
            IConfigSource startupSource = configSource;
            string iniFile = startupSource.Configs["Startup"].GetString("inifile", "NA");
            if (iniFile != "NA")
            {
                //a ini is set to be used for startup settings
                string iniFilePath = Path.Combine(Util.configDir(), iniFile);
                if (File.Exists(iniFilePath))
                {
                    startupSource = new IniConfigSource(iniFilePath);

                    //enable follow line, if we want the original config source(normally commandline args) merged with ini file settings.
                    //in this case we have it so if both sources have the same named setting, command line value will overwrite the ini file value. 
                    //(as if someone has bothered to enter a command line arg, we should take notice of it)
                    //startupSource.Merge(configSource); 
                }
            }
            ReadConfigSettings(startupSource);
        }

        protected void ReadConfigSettings(IConfigSource configSource)
        {
            m_useConfigFile = configSource.Configs["Startup"].GetBoolean("configfile", false);
            m_sandbox = !configSource.Configs["Startup"].GetBoolean("gridmode", false);
            m_physicsEngine = configSource.Configs["Startup"].GetString("physics", "basicphysics");
            m_configFileName = configSource.Configs["Startup"].GetString("config", "simconfig.xml");
            m_silent = configSource.Configs["Startup"].GetBoolean("noverbose", false);
        }

        /// <summary>
        /// Performs initialisation of the scene, such as loading configuration from disk.
        /// </summary>
        public override void StartUp()
        {
            if (!Directory.Exists(Util.logDir()))
            {
                Directory.CreateDirectory(Util.logDir());
            }
            m_log = new LogBase(Path.Combine(Util.logDir(), m_logFilename), "Region", this, m_silent);
            MainLog.Instance = m_log;

            base.StartUp();

            if (!m_sandbox)
            {
                m_httpServer.AddStreamHandler(new SimStatusHandler());
            }

            if (m_sandbox)
            {
                m_commsManager = new CommunicationsLocal(m_networkServersInfo, m_httpServer, m_assetCache);
            }
            else
            {
                m_commsManager = new CommunicationsOGS1(m_networkServersInfo, m_httpServer, m_assetCache);
            }


            string path = Path.Combine(Util.configDir(), "Regions");
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            string[] configFiles = Directory.GetFiles(path, "*.xml");

            if (configFiles.Length == 0)
            {
                string path2 = Path.Combine(Util.configDir(), "Regions");
                string path3 = Path.Combine(path2, "default.xml");

                RegionInfo regionInfo = new RegionInfo("DEFAULT REGION CONFIG", path3);
                configFiles = Directory.GetFiles(path, "*.xml");
            }

            for (int i = 0; i < configFiles.Length; i++)
            {
                //Console.WriteLine("Loading region config file");
                RegionInfo regionInfo = new RegionInfo("REGION CONFIG #" + (i + 1), configFiles[i]);

                UDPServer udpServer;
                Scene scene = SetupScene(regionInfo, out udpServer);

                m_localScenes.Add(scene);


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
            m_networkServersInfo = new NetworkServersInfo("NETWORK SERVERS INFO", Path.Combine(Util.configDir(), "network_servers_information.xml"));
            m_httpServerPort = m_networkServersInfo.HttpListenerPort;
            m_assetCache = new AssetCache("OpenSim.Region.GridInterfaces.Local.dll", m_networkServersInfo.AssetURL, m_networkServersInfo.AssetSendKey);
        }

        protected override LogBase CreateLog()
        {
            if (!Directory.Exists(Util.logDir()))
            {
                Directory.CreateDirectory(Util.logDir());
            }

            return new LogBase((Path.Combine(Util.logDir(), m_logFilename)), "Region", this, m_silent);
        }

        # region Setup methods

        protected override PhysicsScene GetPhysicsScene()
        {
            return GetPhysicsScene(m_physicsEngine);
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
            m_log.Verbose("Closing all threads");
            m_log.Verbose("Killing listener thread");
            m_log.Verbose("Killing clients");
            // IMPLEMENT THIS
            m_log.Verbose("Closing console and terminating");
            for (int i = 0; i < m_localScenes.Count; i++)
            {
                ((Scene)m_localScenes[i]).Close();
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
                    m_log.Error("alert - send alert to a designated user or all users.");
                    m_log.Error("  alert [First] [Last] [Message] - send an alert to a user. Case sensitive.");
                    m_log.Error("  alert general [Message] - send an alert to all users.");
                    m_log.Error("backup - trigger a simulator backup");
                    m_log.Error("script - manually trigger scripts? or script commands?");
                    m_log.Error("show uptime - show simulator startup and uptime.");
                    m_log.Error("show users - show info about connected users.");
                    m_log.Error("shutdown - disconnect all clients and shutdown.");
                    m_log.Error("terrain help - show help for terrain commands.");
                    m_log.Error("quit - equivalent to shutdown.");
                    break;

                case "show":
                    if (cmdparams.Length > 0)
                    {
                        Show(cmdparams[0]);
                    }
                    break;

                case "terrain":
                    string result = "";
                    for (int i = 0; i < m_localScenes.Count; i++)
                    {
                        if (!((Scene)m_localScenes[i]).Terrain.RunTerrainCmd(cmdparams, ref result, m_localScenes[i].RegionInfo.RegionName))
                        {
                            m_log.Error(result);
                        }
                    }
                    break;
                case "script":
                    for (int i = 0; i < m_localScenes.Count; i++)
                    {
                        ((Scene)m_localScenes[i]).SendCommandToScripts(cmdparams);
                    }
                    break;

                case "backup":
                    for (int i = 0; i < m_localScenes.Count; i++)
                    {
                        ((Scene)m_localScenes[i]).Backup();
                    }
                    break;

                case "alert":
                    for (int i = 0; i < m_localScenes.Count; i++)
                    {
                        ((Scene)m_localScenes[i]).HandleAlertCommand(cmdparams);
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
                    for (int i = 0; i < m_localScenes.Count; i++)
                    {
                        foreach (libsecondlife.LLUUID UUID in ((Scene)m_localScenes[i]).Entities.Keys)
                        {
                            if (((Scene)m_localScenes[i]).Entities[UUID].ToString() == "OpenSim.world.Avatar")
                            {
                                TempAv = (ScenePresence)((Scene)m_localScenes[i]).Entities[UUID];
                                m_log.Error(String.Format("{0,-16}{1,-16}{2,-25}{3,-25}{4,-16},{5,-16}{6,-16}", TempAv.firstname, TempAv.lastname, UUID, TempAv.ControllingClient.AgentId, "Unknown", "Unknown"), ((Scene)m_localScenes[i]).RegionInfo.RegionName);
                            }
                        }
                    }
                    break;
            }
        }
        #endregion
    }


}
