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
 */

using System.Collections.Generic;
using System.Net;
using System.Reflection;
using libsecondlife;
using log4net;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Servers;
using OpenSim.Region.Environment;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Physics.Manager;

namespace OpenSim.Region.ClientStack
{
    public abstract class RegionApplicationBase : BaseOpenSimServer
    {
        private static readonly ILog m_log
            = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected AssetCache m_assetCache;
        protected Dictionary<EndPoint, uint> m_clientCircuits = new Dictionary<EndPoint, uint>();
        protected NetworkServersInfo m_networkServersInfo;

        protected BaseHttpServer m_httpServer;
        protected uint m_httpServerPort;

        protected CommunicationsManager m_commsManager;
        public CommunicationsManager CommunicationsManager {
            get { return m_commsManager; }
        }

        protected SceneManager m_sceneManager = new SceneManager();

        protected StorageManager m_storageManager;
        protected string m_storageConnectionString;
        protected string m_estateConnectionString;

        protected ClientStackManager m_clientStackManager;

        public SceneManager SceneManager
        {
            get { return m_sceneManager; }
        }

        public override void Startup()
        {
            base.Startup();
            
            m_storageManager = CreateStorageManager(m_storageConnectionString, m_estateConnectionString);

            m_clientStackManager = CreateClientStackManager();

            Initialize();

            m_httpServer = new BaseHttpServer(m_httpServerPort);

            m_log.Info("[REGION]: Starting HTTP server");

            m_httpServer.Start();
        }

        protected abstract void Initialize();

        // protected void StartConsole()
        // {
        //     m_console = CreateConsole();
        //     MainConsole.Instance = m_console;
        // }

        // protected abstract ConsoleBase CreateConsole();
        protected abstract PhysicsScene GetPhysicsScene();
        protected abstract StorageManager CreateStorageManager(string connectionstring, string estateconnectionstring);
        protected abstract ClientStackManager CreateClientStackManager();

        protected PhysicsScene GetPhysicsScene(string engine, string meshEngine, IConfigSource config)
        {
            PhysicsPluginManager physicsPluginManager;
            physicsPluginManager = new PhysicsPluginManager();
            physicsPluginManager.LoadPlugins();
            return physicsPluginManager.GetPhysicsScene(engine, meshEngine, config);
        }

        protected Scene SetupScene(RegionInfo regionInfo, out IClientNetworkServer clientServer)
        {
            return SetupScene(regionInfo, 0, out clientServer);
        }

        protected Scene SetupScene(RegionInfo regionInfo, int proxyOffset, out IClientNetworkServer clientServer)
        {
            AgentCircuitManager circuitManager = new AgentCircuitManager();
            IPAddress listenIP = regionInfo.InternalEndPoint.Address;
            //if (!IPAddress.TryParse(regionInfo.InternalEndPoint, out listenIP))
            //    listenIP = IPAddress.Parse("0.0.0.0");

            uint port = (uint) regionInfo.InternalEndPoint.Port;
            clientServer = m_clientStackManager.CreateServer(listenIP, ref port, proxyOffset, regionInfo.m_allow_alternate_ports, m_assetCache, circuitManager);
            regionInfo.InternalEndPoint.Port = (int)port;

            Scene scene = CreateScene(regionInfo, m_storageManager, circuitManager);

            clientServer.AddScene(scene);

            scene.LoadWorldMap();

            scene.PhysicsScene = GetPhysicsScene();
            scene.PhysicsScene.SetTerrain(scene.Heightmap.GetFloatsSerialised());
            scene.PhysicsScene.SetWaterLevel((float)regionInfo.RegionSettings.WaterHeight);

            //Master Avatar Setup
            UserProfileData masterAvatar;
            if (scene.RegionInfo.MasterAvatarAssignedUUID != LLUUID.Zero)
            {
                masterAvatar = m_commsManager.UserService.SetupMasterUser(scene.RegionInfo.MasterAvatarAssignedUUID);
            }
            else
            {
                masterAvatar =
                    m_commsManager.UserService.SetupMasterUser(scene.RegionInfo.MasterAvatarFirstName,
                                                               scene.RegionInfo.MasterAvatarLastName,
                                                               scene.RegionInfo.MasterAvatarSandboxPassword);
            }

            if (masterAvatar != null)
            {
                m_log.Info("[PARCEL]: Found master avatar [" + masterAvatar.ID.ToString() + "]");
                scene.RegionInfo.MasterAvatarAssignedUUID = masterAvatar.ID;
            }
            else
            {
                m_log.Info("[PARCEL]: No master avatar found, using null.");
                scene.RegionInfo.MasterAvatarAssignedUUID = LLUUID.Zero;
            }

            scene.LoadPrimsFromStorage(regionInfo.originRegionID);
            scene.StartTimer();

            return scene;
        }

        protected abstract Scene CreateScene(RegionInfo regionInfo, StorageManager storageManager,
                                             AgentCircuitManager circuitManager);
    }
}
