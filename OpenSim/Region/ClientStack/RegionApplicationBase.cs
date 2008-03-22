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

using System;
using System.Collections.Generic;
using System.Net;
using libsecondlife;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Console;
using OpenSim.Framework.Servers;
using OpenSim.Region.Environment;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Physics.Manager;

namespace OpenSim.Region.ClientStack
{
    public abstract class RegionApplicationBase : BaseOpenSimServer
    {
        private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        protected AssetCache m_assetCache;
        protected Dictionary<EndPoint, uint> m_clientCircuits = new Dictionary<EndPoint, uint>();
        protected NetworkServersInfo m_networkServersInfo;

        protected BaseHttpServer m_httpServer;
        protected uint m_httpServerPort;

        protected CommunicationsManager m_commsManager;

        protected SceneManager m_sceneManager = new SceneManager();

        protected StorageManager m_storageManager;
        protected string m_storageConnectionString;
        
        // An attribute to indicate whether prim inventories should be persisted.
        // Probably will be temporary until this stops being experimental.
        protected bool m_storagePersistPrimInventories;

        public SceneManager SceneManager
        {
            get { return m_sceneManager; }
        }

        public virtual void StartUp()
        {
            ClientView.TerrainManager = new TerrainManager(new SecondLife());

            m_storageManager = CreateStorageManager(m_storageConnectionString);

            Initialize();

            m_httpServer = new BaseHttpServer(m_httpServerPort);

            m_log.Info("[REGION]: Starting HTTP server");

            m_httpServer.Start();
        }

        protected abstract void Initialize();

        protected void StartConsole()
        {
            m_console = CreateConsole();
            MainConsole.Instance = m_console;
        }

        protected abstract ConsoleBase CreateConsole();
        protected abstract PhysicsScene GetPhysicsScene();
        protected abstract StorageManager CreateStorageManager(string connectionstring);

        protected PhysicsScene GetPhysicsScene(string engine, string meshEngine)
        {
            PhysicsPluginManager physicsPluginManager;
            physicsPluginManager = new PhysicsPluginManager();
            physicsPluginManager.LoadPlugins();
            return physicsPluginManager.GetPhysicsScene(engine, meshEngine);
        }

        protected Scene SetupScene(RegionInfo regionInfo, out UDPServer udpServer, bool m_permissions)
        {
            return SetupScene(regionInfo, 0, out udpServer, m_permissions);
        }

        protected Scene SetupScene(RegionInfo regionInfo, int proxyOffset, out UDPServer udpServer, bool m_permissions)
        {
            AgentCircuitManager circuitManager = new AgentCircuitManager();
            IPAddress listenIP = regionInfo.InternalEndPoint.Address;
            //if (!IPAddress.TryParse(regionInfo.InternalEndPoint, out listenIP))
            //    listenIP = IPAddress.Parse("0.0.0.0");

            uint port = (uint) regionInfo.InternalEndPoint.Port;
            udpServer = new UDPServer(listenIP, ref port, proxyOffset, regionInfo.m_allow_alternate_ports, m_assetCache, circuitManager);
            regionInfo.InternalEndPoint.Port = (int)port;

            Scene scene = CreateScene(regionInfo, m_storageManager, circuitManager);
            
            udpServer.LocalScene = scene;

            scene.LoadWorldMap();
            scene.RegisterRegionWithGrid();

            scene.PhysicsScene = GetPhysicsScene();
            scene.PhysicsScene.SetTerrain(scene.Heightmap.GetFloatsSerialised());
            scene.PhysicsScene.SetWaterLevel(regionInfo.EstateSettings.waterHeight);

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
                m_log.Info("[PARCEL]: Found master avatar [" + masterAvatar.UUID.ToString() + "]");
                scene.RegionInfo.MasterAvatarAssignedUUID = masterAvatar.UUID;
            }
            else
            {
                m_log.Info("[PARCEL]: No master avatar found, using null.");
                scene.RegionInfo.MasterAvatarAssignedUUID = LLUUID.Zero;
            }

            scene.LoadPrimsFromStorage(m_permissions, regionInfo.originRegionID);
            scene.StartTimer();
            return scene;
        }

        protected abstract Scene CreateScene(RegionInfo regionInfo, StorageManager storageManager,
                                             AgentCircuitManager circuitManager);
    }
}
