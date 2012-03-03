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
 *     * Neither the name of the OpenSimulator Project nor the
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
using System.Net;
using System.Collections.Generic;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Console;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Physics.Manager;
using OpenSim.Region.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.CoreModules.Avatar.Gods;
using OpenSim.Region.CoreModules.Asset;
using OpenSim.Region.CoreModules.ServiceConnectorsOut.Asset;
using OpenSim.Region.CoreModules.ServiceConnectorsOut.Authentication;
using OpenSim.Region.CoreModules.ServiceConnectorsOut.Inventory;
using OpenSim.Region.CoreModules.ServiceConnectorsOut.Grid;
using OpenSim.Region.CoreModules.ServiceConnectorsOut.UserAccounts;
using OpenSim.Region.CoreModules.ServiceConnectorsOut.Presence;
using OpenSim.Services.Interfaces;
using OpenSim.Tests.Common.Mock;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;

namespace OpenSim.Tests.Common
{
    /// <summary>
    /// Helpers for setting up scenes.
    /// </summary>
    public class SceneHelpers
    {
        public static TestScene SetupScene()
        {
            return SetupScene(null);
        }

        /// <summary>
        /// Set up a test scene
        /// </summary>
        /// <remarks>
        /// Automatically starts service threads, as would the normal runtime.
        /// </remarks>
        /// <returns></returns>
        public static TestScene SetupScene(CoreAssetCache cache)
        {
            return SetupScene("Unit test region", UUID.Random(), 1000, 1000, cache);
        }

        public static TestScene SetupScene(string name, UUID id, uint x, uint y)
        {
            return SetupScene(name, id, x, y, null);
        }

        /// <summary>
        /// Set up a scene. If it's more then one scene, use the same CommunicationsManager to link regions
        /// or a different, to get a brand new scene with new shared region modules.
        /// </summary>
        /// <param name="name">Name of the region</param>
        /// <param name="id">ID of the region</param>
        /// <param name="x">X co-ordinate of the region</param>
        /// <param name="y">Y co-ordinate of the region</param>
        /// <param name="cache"></param>
        /// <returns></returns>
        public static TestScene SetupScene(
            string name, UUID id, uint x, uint y, CoreAssetCache cache)
        {
            return SetupScene(name, id, x, y, cache, new IniConfigSource());
        }

        /// <summary>
        /// Set up a scene. If it's more then one scene, use the same CommunicationsManager to link regions
        /// or a different, to get a brand new scene with new shared region modules.
        /// </summary>
        /// <param name="name">Name of the region</param>
        /// <param name="id">ID of the region</param>
        /// <param name="x">X co-ordinate of the region</param>
        /// <param name="y">Y co-ordinate of the region</param>
        /// <param name="cache"></param>
        /// <param name="configSource"></param>
        /// <returns></returns>
        public static TestScene SetupScene(
            string name, UUID id, uint x, uint y, CoreAssetCache cache, IConfigSource configSource)
        {
            Console.WriteLine("Setting up test scene {0}", name);

            // We must set up a console otherwise setup of some modules may fail
            MainConsole.Instance = new MockConsole();
            
            RegionInfo regInfo = new RegionInfo(x, y, new IPEndPoint(IPAddress.Loopback, 9000), "127.0.0.1");
            regInfo.RegionName = name;
            regInfo.RegionID = id;

            AgentCircuitManager acm = new AgentCircuitManager();
            SceneCommunicationService scs = new SceneCommunicationService();

            ISimulationDataService simDataService = OpenSim.Server.Base.ServerUtils.LoadPlugin<ISimulationDataService>("OpenSim.Tests.Common.dll", null);
            IEstateDataService estateDataService = null;

            TestScene testScene = new TestScene(
                regInfo, acm, scs, simDataService, estateDataService, null, false, configSource, null);

            IRegionModule godsModule = new GodsModule();
            godsModule.Initialise(testScene, new IniConfigSource());
            testScene.AddModule(godsModule.Name, godsModule);

            LocalAssetServicesConnector       assetService       = StartAssetService(testScene, cache);
                                                                   StartAuthenticationService(testScene);
            LocalInventoryServicesConnector   inventoryService   = StartInventoryService(testScene);
                                                                   StartGridService(testScene);
            LocalUserAccountServicesConnector userAccountService = StartUserAccountService(testScene);            
            LocalPresenceServicesConnector    presenceService    = StartPresenceService(testScene);

            inventoryService.PostInitialise();
            assetService.PostInitialise();
            userAccountService.PostInitialise();
            presenceService.PostInitialise();
            
            testScene.RegionInfo.EstateSettings.EstateOwner = UUID.Random();
            testScene.SetModuleInterfaces();

            testScene.LandChannel = new TestLandChannel(testScene);
            testScene.LoadWorldMap();

            PhysicsPluginManager physicsPluginManager = new PhysicsPluginManager();
            physicsPluginManager.LoadPluginsFromAssembly("Physics/OpenSim.Region.Physics.BasicPhysicsPlugin.dll");
            testScene.PhysicsScene
                = physicsPluginManager.GetPhysicsScene("basicphysics", "ZeroMesher",   new IniConfigSource(), "test");

            testScene.RegionInfo.EstateSettings = new EstateSettings();
            testScene.LoginsDisabled = false;
            testScene.RegisterRegionWithGrid();

            return testScene;
        }

        private static LocalAssetServicesConnector StartAssetService(Scene testScene, CoreAssetCache cache)
        {
            LocalAssetServicesConnector assetService = new LocalAssetServicesConnector();
            IConfigSource config = new IniConfigSource();
            
            config.AddConfig("Modules");            
            config.Configs["Modules"].Set("AssetServices", "LocalAssetServicesConnector");            
            config.AddConfig("AssetService");
            config.Configs["AssetService"].Set("LocalServiceModule", "OpenSim.Services.AssetService.dll:AssetService");            
            config.Configs["AssetService"].Set("StorageProvider", "OpenSim.Tests.Common.dll");
            
            assetService.Initialise(config);
            assetService.AddRegion(testScene);

            if (cache != null)
            {
                IConfigSource cacheConfig = new IniConfigSource();
                cacheConfig.AddConfig("Modules");
                cacheConfig.Configs["Modules"].Set("AssetCaching", "CoreAssetCache");
                cacheConfig.AddConfig("AssetCache");

                cache.Initialise(cacheConfig);
                cache.AddRegion(testScene);
                cache.RegionLoaded(testScene);
                testScene.AddRegionModule(cache.Name, cache);
            }

            assetService.RegionLoaded(testScene);
            testScene.AddRegionModule(assetService.Name, assetService);
            
            return assetService;
        }

        private static void StartAuthenticationService(Scene testScene)
        {
            ISharedRegionModule service = new LocalAuthenticationServicesConnector();
            IConfigSource config = new IniConfigSource();
            
            config.AddConfig("Modules");
            config.AddConfig("AuthenticationService");
            config.Configs["Modules"].Set("AuthenticationServices", "LocalAuthenticationServicesConnector");
            config.Configs["AuthenticationService"].Set(
                "LocalServiceModule", "OpenSim.Services.AuthenticationService.dll:PasswordAuthenticationService");
            config.Configs["AuthenticationService"].Set("StorageProvider", "OpenSim.Data.Null.dll");
            
            service.Initialise(config);
            service.AddRegion(testScene);
            service.RegionLoaded(testScene);
            testScene.AddRegionModule(service.Name, service);
            //m_authenticationService = service;
        }

        private static LocalInventoryServicesConnector StartInventoryService(Scene testScene)
        {
            LocalInventoryServicesConnector inventoryService = new LocalInventoryServicesConnector();
            
            IConfigSource config = new IniConfigSource();            
            config.AddConfig("Modules");
            config.AddConfig("InventoryService");
            config.Configs["Modules"].Set("InventoryServices", "LocalInventoryServicesConnector");
            config.Configs["InventoryService"].Set("LocalServiceModule", "OpenSim.Services.InventoryService.dll:InventoryService");
            config.Configs["InventoryService"].Set("StorageProvider", "OpenSim.Tests.Common.dll");
            
            inventoryService.Initialise(config);
            inventoryService.AddRegion(testScene);
            inventoryService.RegionLoaded(testScene);
            testScene.AddRegionModule(inventoryService.Name, inventoryService);
            
            return inventoryService;           
        }

        private static LocalGridServicesConnector StartGridService(Scene testScene)
        {
            IConfigSource config = new IniConfigSource();
            config.AddConfig("Modules");
            config.AddConfig("GridService");
            config.Configs["Modules"].Set("GridServices", "LocalGridServicesConnector");
            config.Configs["GridService"].Set("StorageProvider", "OpenSim.Data.Null.dll:NullRegionData");
            config.Configs["GridService"].Set("LocalServiceModule", "OpenSim.Services.GridService.dll:GridService");
            config.Configs["GridService"].Set("ConnectionString", "!static");

            LocalGridServicesConnector gridService = new LocalGridServicesConnector();
            gridService.Initialise(config);
            gridService.AddRegion(testScene);
            gridService.RegionLoaded(testScene);
            
            return gridService;
        }

        /// <summary>
        /// Start a user account service
        /// </summary>
        /// <param name="testScene"></param>
        /// <returns></returns>
        private static LocalUserAccountServicesConnector StartUserAccountService(Scene testScene)
        {
            IConfigSource config = new IniConfigSource();
            config.AddConfig("Modules");
            config.AddConfig("UserAccountService");
            config.Configs["Modules"].Set("UserAccountServices", "LocalUserAccountServicesConnector");
            config.Configs["UserAccountService"].Set("StorageProvider", "OpenSim.Data.Null.dll");
            config.Configs["UserAccountService"].Set(
                "LocalServiceModule", "OpenSim.Services.UserAccountService.dll:UserAccountService");

            LocalUserAccountServicesConnector userAccountService = new LocalUserAccountServicesConnector();
            userAccountService.Initialise(config);

            userAccountService.AddRegion(testScene);
            userAccountService.RegionLoaded(testScene);
            testScene.AddRegionModule(userAccountService.Name, userAccountService);
            
            return userAccountService;
        }

        /// <summary>
        /// Start a presence service
        /// </summary>
        /// <param name="testScene"></param>
        private static LocalPresenceServicesConnector StartPresenceService(Scene testScene)
        {
            IConfigSource config = new IniConfigSource();
            config.AddConfig("Modules");
            config.AddConfig("PresenceService");
            config.Configs["Modules"].Set("PresenceServices", "LocalPresenceServicesConnector");
            config.Configs["PresenceService"].Set("StorageProvider", "OpenSim.Data.Null.dll");
            config.Configs["PresenceService"].Set(
                "LocalServiceModule", "OpenSim.Services.PresenceService.dll:PresenceService");

            LocalPresenceServicesConnector presenceService = new LocalPresenceServicesConnector();
            presenceService.Initialise(config);

            presenceService.AddRegion(testScene);
            presenceService.RegionLoaded(testScene);
            testScene.AddRegionModule(presenceService.Name, presenceService);
            
            return presenceService;
        }

        /// <summary>
        /// Setup modules for a scene using their default settings.
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="modules"></param>
        public static void SetupSceneModules(Scene scene, params object[] modules)
        {
            SetupSceneModules(scene, new IniConfigSource(), modules);
        }

        /// <summary>
        /// Setup modules for a scene.
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="config"></param>
        /// <param name="modules"></param>
        public static void SetupSceneModules(Scene scene, IConfigSource config, params object[] modules)
        {
            List<IRegionModuleBase> newModules = new List<IRegionModuleBase>();
            foreach (object module in modules)
            {
                if (module is IRegionModule)
                {
                    IRegionModule m = (IRegionModule)module;
                    m.Initialise(scene, config);
                    scene.AddModule(m.Name, m);
                    m.PostInitialise();
                }
                else if (module is IRegionModuleBase)
                {
                    // for the new system, everything has to be initialised first,
                    // shared modules have to be post-initialised, then all get an AddRegion with the scene
                    IRegionModuleBase m = (IRegionModuleBase)module;
                    m.Initialise(config);
                    newModules.Add(m);
                }
            }

            foreach (IRegionModuleBase module in newModules)
            {
                if (module is ISharedRegionModule) ((ISharedRegionModule)module).PostInitialise();
            }

            foreach (IRegionModuleBase module in newModules)
            {
                module.AddRegion(scene);
                scene.AddRegionModule(module.Name, module);
            }
            
            // RegionLoaded is fired after all modules have been appropriately added to all scenes
            foreach (IRegionModuleBase module in newModules)
                module.RegionLoaded(scene);                

            scene.SetModuleInterfaces();
        }

        /// <summary>
        /// Generate some standard agent connection data.
        /// </summary>
        /// <param name="agentId"></param>
        /// <returns></returns>
        public static AgentCircuitData GenerateAgentData(UUID agentId)
        {
            string firstName = "testfirstname";

            AgentCircuitData agentData = new AgentCircuitData();
            agentData.AgentID = agentId;
            agentData.firstname = firstName;
            agentData.lastname = "testlastname";
            agentData.SessionID = UUID.Zero;
            agentData.SecureSessionID = UUID.Zero;
            agentData.circuitcode = 123;
            agentData.BaseFolder = UUID.Zero;
            agentData.InventoryFolder = UUID.Zero;
            agentData.startpos = Vector3.Zero;
            agentData.CapsPath = "http://wibble.com";
            agentData.ServiceURLs = new Dictionary<string, object>();
            agentData.Appearance = new AvatarAppearance();

            return agentData;
        }

        /// <summary>
        /// Add a root agent where the details of the agent connection (apart from the id) are unimportant for the test
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="agentId"></param>
        /// <returns></returns>
        public static ScenePresence AddScenePresence(Scene scene, UUID agentId)
        {
            return AddScenePresence(scene, GenerateAgentData(agentId));
        }

        /// <summary>
        /// Add a root agent.
        /// </summary>
        /// <remarks>
        /// This function
        ///
        /// 1)  Tells the scene that an agent is coming.  Normally, the login service (local if standalone, from the
        /// userserver if grid) would give initial login data back to the client and separately tell the scene that the
        /// agent was coming.
        ///
        /// 2)  Connects the agent with the scene
        ///
        /// This function performs actions equivalent with notifying the scene that an agent is
        /// coming and then actually connecting the agent to the scene.  The one step missed out is the very first
        /// </remarks>
        /// <param name="scene"></param>
        /// <param name="agentData"></param>
        /// <returns></returns>
        public static ScenePresence AddScenePresence(Scene scene, AgentCircuitData agentData)
        {
            // We emulate the proper login sequence here by doing things in four stages

            // Stage 0: login
            scene.PresenceService.LoginAgent(agentData.AgentID.ToString(), agentData.SessionID, agentData.SecureSessionID);

            // Stages 1 & 2
            ScenePresence sp = IntroduceClientToScene(scene, agentData, TeleportFlags.ViaLogin);

            // Stage 3: Complete the entrance into the region.  This converts the child agent into a root agent.
            sp.CompleteMovement(sp.ControllingClient, true);

            return sp;
        }

        private static ScenePresence IntroduceClientToScene(Scene scene, AgentCircuitData agentData, TeleportFlags tf)
        {
            string reason;

            // Stage 1: tell the scene to expect a new user connection
            if (!scene.NewUserConnection(agentData, (uint)tf, out reason))
                Console.WriteLine("NewUserConnection failed: " + reason);

            // Stage 2: add the new client as a child agent to the scene
            TestClient client = new TestClient(agentData, scene);
            scene.AddNewClient(client, PresenceType.User);

            return scene.GetScenePresence(agentData.AgentID);
        }

        public static ScenePresence AddChildScenePresence(Scene scene, UUID agentId)
        {
            AgentCircuitData acd = GenerateAgentData(agentId);
            acd.child = true;

            // XXX: ViaLogin may not be correct for child agents
            return IntroduceClientToScene(scene, acd, TeleportFlags.ViaLogin);
        }

        /// <summary>
        /// Add a test object
        /// </summary>
        /// <param name="scene"></param>
        /// <returns></returns>
        public static SceneObjectPart AddSceneObject(Scene scene)
        {
            return AddSceneObject(scene, "Test Object");
        }

        /// <summary>
        /// Add a test object
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public static SceneObjectPart AddSceneObject(Scene scene, string name)
        {
            SceneObjectPart part = CreateSceneObjectPart(name, UUID.Random(), UUID.Zero);

            //part.UpdatePrimFlags(false, false, true);
            //part.ObjectFlags |= (uint)PrimFlags.Phantom;

            scene.AddNewSceneObject(new SceneObjectGroup(part), false);

            return part;
        }
        
        /// <summary>
        /// Create a scene object part.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="id"></param>
        /// <param name="ownerId"></param>
        /// <returns></returns>
        public static SceneObjectPart CreateSceneObjectPart(string name, UUID id, UUID ownerId)
        {            
            return new SceneObjectPart(
                ownerId, PrimitiveBaseShape.Default, Vector3.Zero, Quaternion.Identity, Vector3.Zero) 
                    { Name = name, UUID = id, Scale = new Vector3(1, 1, 1) };            
        }
        
        /// <summary>
        /// Create a scene object but do not add it to the scene.
        /// </summary>
        /// <remarks>
        /// UUID always starts at 00000000-0000-0000-0000-000000000001
        /// </remarks>
        /// <param name="parts">The number of parts that should be in the scene object</param>
        /// <param name="ownerId"></param>
        /// <returns></returns>
        public static SceneObjectGroup CreateSceneObject(int parts, UUID ownerId)
        {            
            return CreateSceneObject(parts, ownerId, "", 0x1);
        }          
        
        /// <summary>
        /// Create a scene object but do not add it to the scene.
        /// </summary>
        /// <param name="parts">
        /// The number of parts that should be in the scene object
        /// </param>
        /// <param name="ownerId"></param>
        /// <param name="partNamePrefix">
        /// The prefix to be given to part names.  This will be suffixed with "Part<part no>"
        /// (e.g. mynamePart0 for the root part)
        /// </param>
        /// <param name="uuidTail">
        /// The hexadecimal last part of the UUID for parts created.  A UUID of the form "00000000-0000-0000-0000-{0:XD12}"
        /// will be given to the root part, and incremented for each part thereafter.
        /// </param>
        /// <returns></returns>
        public static SceneObjectGroup CreateSceneObject(int parts, UUID ownerId, string partNamePrefix, int uuidTail)
        {            
            string rawSogId = string.Format("00000000-0000-0000-0000-{0:X12}", uuidTail);
            
            SceneObjectGroup sog 
                = new SceneObjectGroup(
                    CreateSceneObjectPart(string.Format("{0}Part0", partNamePrefix), new UUID(rawSogId), ownerId));
            
            if (parts > 1)
                for (int i = 1; i < parts; i++)
                    sog.AddPart(
                        CreateSceneObjectPart(
                            string.Format("{0}Part{1}", partNamePrefix, i), 
                            new UUID(string.Format("00000000-0000-0000-0000-{0:X12}", uuidTail + i)), 
                            ownerId));
            
            return sog;
        }        
    }
}