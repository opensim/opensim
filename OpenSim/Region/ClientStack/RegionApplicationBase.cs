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
using System.IO;
using System.Net;
using System.Reflection;
using OpenMetaverse;
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
        
        public CommunicationsManager CommunicationsManager 
        {
            get { return m_commsManager; }
        }
        protected CommunicationsManager m_commsManager;        

        protected StorageManager m_storageManager;
        
        protected ClientStackManager m_clientStackManager;

        public SceneManager SceneManager
        {
            get { return m_sceneManager; }
        }
        protected SceneManager m_sceneManager = new SceneManager();
        
        protected abstract void Initialize();
        
        /// <summary>
        /// Get a new physics scene.
        /// </summary>
        /// 
        /// <param name="osSceneIdentifier">
        /// The name of the OpenSim scene this physics scene is serving.  This will be used in log messages.  
        /// </param>
        /// <returns></returns>        
        protected abstract PhysicsScene GetPhysicsScene(string osSceneIdentifier);
        
        protected abstract StorageManager CreateStorageManager();
        protected abstract ClientStackManager CreateClientStackManager();
        protected abstract Scene CreateScene(RegionInfo regionInfo, StorageManager storageManager,
                                             AgentCircuitManager circuitManager);        

        protected override void StartupSpecific()
        {
            m_storageManager = CreateStorageManager();

            m_clientStackManager = CreateClientStackManager();

            Initialize();

            m_httpServer 
                = new BaseHttpServer(
                    m_httpServerPort, m_networkServersInfo.HttpUsesSSL, m_networkServersInfo.httpSSLPort, 
                    m_networkServersInfo.HttpSSLCN);
            
            if (m_networkServersInfo.HttpUsesSSL && (m_networkServersInfo.HttpListenerPort == m_networkServersInfo.httpSSLPort))
            {
                m_log.Error("[HTTP]: HTTP Server config failed.   HTTP Server and HTTPS server must be on different ports");
            }

            m_log.Info("[REGION]: Starting HTTP server");
            m_httpServer.Start();
        }

        /// <summary>
        /// Get a new physics scene.
        /// </summary>
        /// <param name="engine">The name of the physics engine to use</param>
        /// <param name="meshEngine">The name of the mesh engine to use</param>
        /// <param name="config">The configuration data to pass to the physics and mesh engines</param>
        /// <param name="osSceneIdentifier">
        /// The name of the OpenSim scene this physics scene is serving.  This will be used in log messages.  
        /// </param>
        /// <returns></returns>
        protected PhysicsScene GetPhysicsScene(
            string engine, string meshEngine, IConfigSource config, string osSceneIdentifier)
        {
            PhysicsPluginManager physicsPluginManager;
            physicsPluginManager = new PhysicsPluginManager();
            physicsPluginManager.LoadPluginsFromAssemblies("Physics");
            
            return physicsPluginManager.GetPhysicsScene(engine, meshEngine, config, osSceneIdentifier);
        }

        /// <summary>
        /// Create a scene and its initial base structures.
        /// </summary>
        /// <param name="regionInfo"></param>
        /// <param name="clientServer"> </param>
        /// <returns></returns>        
        protected Scene SetupScene(RegionInfo regionInfo, out IClientNetworkServer clientServer)
        {
            return SetupScene(regionInfo, 0, null, out clientServer);
        }

        /// <summary>
        /// Create a scene and its initial base structures.
        /// </summary>
        /// TODO: Really configSource shouldn't be passed in here, but should be moved up to BaseOpenSimServer and 
        /// made common to all the servers.
        /// 
        /// <param name="regionInfo"></param>
        /// <param name="proxyOffset"></param>
        /// <param name="configSource"></param>
        /// <param name="clientServer"> </param>
        /// <returns></returns>
        protected Scene SetupScene(
            RegionInfo regionInfo, int proxyOffset, IConfigSource configSource, out IClientNetworkServer clientServer)
        {
            AgentCircuitManager circuitManager = new AgentCircuitManager();
            IPAddress listenIP = regionInfo.InternalEndPoint.Address;
            //if (!IPAddress.TryParse(regionInfo.InternalEndPoint, out listenIP))
            //    listenIP = IPAddress.Parse("0.0.0.0");

            uint port = (uint) regionInfo.InternalEndPoint.Port;
            
            clientServer 
                = m_clientStackManager.CreateServer(
                    listenIP, ref port, proxyOffset, regionInfo.m_allow_alternate_ports, configSource,
                    m_assetCache, circuitManager);
            
            regionInfo.InternalEndPoint.Port = (int)port;

            Scene scene = CreateScene(regionInfo, m_storageManager, circuitManager);

            clientServer.AddScene(scene);

            scene.LoadWorldMap();

            scene.PhysicsScene = GetPhysicsScene(scene.RegionInfo.RegionName);
            scene.PhysicsScene.SetTerrain(scene.Heightmap.GetFloatsSerialised());
            scene.PhysicsScene.SetWaterLevel((float)regionInfo.RegionSettings.WaterHeight);

            // TODO: Remove this cruft once MasterAvatar is fully deprecated
            //Master Avatar Setup
            UserProfileData masterAvatar;
            if (scene.RegionInfo.MasterAvatarAssignedUUID == UUID.Zero)
            {
                masterAvatar =
                    m_commsManager.UserService.SetupMasterUser(scene.RegionInfo.MasterAvatarFirstName,
                                                               scene.RegionInfo.MasterAvatarLastName,
                                                               scene.RegionInfo.MasterAvatarSandboxPassword);
            }
            else
            {
                masterAvatar = m_commsManager.UserService.SetupMasterUser(scene.RegionInfo.MasterAvatarAssignedUUID);
                scene.RegionInfo.MasterAvatarFirstName = masterAvatar.FirstName;
                scene.RegionInfo.MasterAvatarLastName = masterAvatar.SurName;
            }

            if (masterAvatar == null)
            {
                m_log.Info("[PARCEL]: No master avatar found, using null.");
                scene.RegionInfo.MasterAvatarAssignedUUID = UUID.Zero;
            }
            else
            {
                m_log.InfoFormat("[PARCEL]: Found master avatar {0} {1} [" + masterAvatar.ID.ToString() + "]",
                                 scene.RegionInfo.MasterAvatarFirstName, scene.RegionInfo.MasterAvatarLastName);
                scene.RegionInfo.MasterAvatarAssignedUUID = masterAvatar.ID;
            }
            
            return scene;
        }
    }
}
