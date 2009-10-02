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
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Console;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Physics.Manager;
using OpenSim.Region.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.CoreModules.Agent.Capabilities;
using OpenSim.Region.CoreModules.Avatar.Gods;
using OpenSim.Region.CoreModules.ServiceConnectorsOut.Asset;
using OpenSim.Region.CoreModules.ServiceConnectorsOut.Inventory;
using OpenSim.Region.CoreModules.ServiceConnectorsOut.Grid;
using OpenSim.Services.Interfaces;
using OpenSim.Tests.Common.Mock;

namespace OpenSim.Tests.Common.Setup
{
    /// <summary>
    /// Helpers for setting up scenes.
    /// </summary>
    public class SceneSetupHelpers
    {
        // These static variables in order to allow regions to be linked by shared modules and same
        // CommunicationsManager. 
        private static ISharedRegionModule m_assetService = null;
        private static ISharedRegionModule m_inventoryService = null;
        private static ISharedRegionModule m_gridService = null;
        private static TestCommunicationsManager commsManager = null;

        /// <summary>
        /// Set up a test scene
        /// </summary>
        /// 
        /// Automatically starts service threads, as would the normal runtime.
        /// 
        /// <returns></returns>
        public static TestScene SetupScene()
        {
            return SetupScene("");
        }
        
        /// <summary>
        /// Set up a test scene
        /// </summary>
        /// 
        /// <param name="realServices">Starts real inventory and asset services, as opposed to mock ones, if true</param>
        /// <returns></returns>
        public static TestScene SetupScene(String realServices)
        {
            return SetupScene(
                "Unit test region", UUID.Random(), 1000, 1000, new TestCommunicationsManager(), realServices);
        }

        /// <summary>
        /// Set up a test scene
        /// </summary>
        /// 
        /// <param name="realServices">Starts real inventory and asset services, as opposed to mock ones, if true</param>
        /// <param name="cm">This should be the same if simulating two scenes within a standalone</param>
        /// <returns></returns>
        public static TestScene SetupScene(TestCommunicationsManager cm, String realServices)
        {
            return SetupScene(
                "Unit test region", UUID.Random(), 1000, 1000, cm, "");
        }
        /// <summary>
        /// Set up a test scene
        /// </summary>
        /// <param name="name">Name of the region</param>
        /// <param name="id">ID of the region</param>
        /// <param name="x">X co-ordinate of the region</param>
        /// <param name="y">Y co-ordinate of the region</param>
        /// <param name="cm">This should be the same if simulating two scenes within a standalone</param>
        /// <returns></returns>
        public static TestScene SetupScene(string name, UUID id, uint x, uint y, TestCommunicationsManager cm)
        {
            return SetupScene(name, id, x, y, cm, "");
        }


        /// <summary>
        /// Set up a scene. If it's more then one scene, use the same CommunicationsManager to link regions
        /// or a different, to get a brand new scene with new shared region modules.
        /// </summary>
        /// <param name="name">Name of the region</param>
        /// <param name="id">ID of the region</param>
        /// <param name="x">X co-ordinate of the region</param>
        /// <param name="y">Y co-ordinate of the region</param>
        /// <param name="cm">This should be the same if simulating two scenes within a standalone</param>
        /// <param name="realServices">Starts real inventory and asset services, as opposed to mock ones, if true</param>
        /// <returns></returns>
        public static TestScene SetupScene(
            string name, UUID id, uint x, uint y, TestCommunicationsManager cm, String realServices)
        {
            bool newScene = false;

            Console.WriteLine("Setting up test scene {0}", name);
            
            // If cm is the same as our last commsManager used, this means the tester wants to link
            // regions. In this case, don't use the sameshared region modules and dont initialize them again.
            // Also, no need to start another MainServer and MainConsole instance.
            if (cm == null || cm != commsManager)
            {
                System.Console.WriteLine("Starting a brand new scene");
                newScene = true;
                MainConsole.Instance = new LocalConsole("TEST PROMPT");
                MainServer.Instance = new BaseHttpServer(980);
                commsManager = cm;
            }

            // We must set up a console otherwise setup of some modules may fail
            RegionInfo regInfo = new RegionInfo(x, y, new IPEndPoint(IPAddress.Loopback, 9000), "127.0.0.1");
            regInfo.RegionName = name;
            regInfo.RegionID = id;

            AgentCircuitManager acm = new AgentCircuitManager();
            SceneCommunicationService scs = new SceneCommunicationService(cm);

            StorageManager sm = new StorageManager("OpenSim.Data.Null.dll", "", "");
            IConfigSource configSource = new IniConfigSource();

            TestScene testScene = new TestScene(
                regInfo, acm, cm, scs, sm, null, false, false, false, configSource, null);

            INonSharedRegionModule capsModule = new CapabilitiesModule();
            capsModule.Initialise(new IniConfigSource());
            testScene.AddRegionModule(capsModule.Name, capsModule);
            capsModule.AddRegion(testScene);
            
            IRegionModule godsModule = new GodsModule();
            godsModule.Initialise(testScene, new IniConfigSource());
            testScene.AddModule(godsModule.Name, godsModule);
            realServices = realServices.ToLower();
            // IConfigSource config = new IniConfigSource();
            
            // If we have a brand new scene, need to initialize shared region modules
            if ((m_assetService == null && m_inventoryService == null) || newScene)
            {
                if (realServices.Contains("asset"))
                    StartAssetService(testScene, true);
                else
                    StartAssetService(testScene, false);
                if (realServices.Contains("inventory"))
                    StartInventoryService(testScene, true);
                else
                    StartInventoryService(testScene, false);
                if (realServices.Contains("grid"))
                    StartGridService(testScene, true);

            }
            // If not, make sure the shared module gets references to this new scene
            else
            {
                m_assetService.AddRegion(testScene);
                m_assetService.RegionLoaded(testScene);
                m_inventoryService.AddRegion(testScene);
                m_inventoryService.RegionLoaded(testScene);
            }
            m_inventoryService.PostInitialise();
            m_assetService.PostInitialise();

            testScene.CommsManager.UserService.SetInventoryService(testScene.InventoryService);

            testScene.SetModuleInterfaces();

            testScene.LandChannel = new TestLandChannel(testScene);
            testScene.LoadWorldMap();

            PhysicsPluginManager physicsPluginManager = new PhysicsPluginManager();
            physicsPluginManager.LoadPluginsFromAssembly("Physics/OpenSim.Region.Physics.BasicPhysicsPlugin.dll");
            testScene.PhysicsScene
                = physicsPluginManager.GetPhysicsScene("basicphysics", "ZeroMesher",   new IniConfigSource(), "test");
            
            return testScene;
        }

        private static void StartAssetService(Scene testScene, bool real)
        {
            ISharedRegionModule assetService = new LocalAssetServicesConnector();
            IConfigSource config = new IniConfigSource();
            config.AddConfig("Modules");
            config.AddConfig("AssetService");
            config.Configs["Modules"].Set("AssetServices", "LocalAssetServicesConnector");
            if (real)
                config.Configs["AssetService"].Set("LocalServiceModule", "OpenSim.Services.AssetService.dll:AssetService");
            else
                config.Configs["AssetService"].Set("LocalServiceModule", "OpenSim.Tests.Common.dll:TestAssetService");
            config.Configs["AssetService"].Set("StorageProvider", "OpenSim.Tests.Common.dll");
            assetService.Initialise(config);
            assetService.AddRegion(testScene);
            assetService.RegionLoaded(testScene);
            testScene.AddRegionModule(assetService.Name, assetService);
            m_assetService = assetService;
        }

        private static void StartInventoryService(Scene testScene, bool real)
        {
            ISharedRegionModule inventoryService = new LocalInventoryServicesConnector();
            IConfigSource config = new IniConfigSource();
            config.AddConfig("Modules");
            config.AddConfig("InventoryService");
            config.Configs["Modules"].Set("InventoryServices", "LocalInventoryServicesConnector");
            if (real)
                config.Configs["InventoryService"].Set("LocalServiceModule", "OpenSim.Services.InventoryService.dll:InventoryService");
            else
                config.Configs["InventoryService"].Set("LocalServiceModule", "OpenSim.Tests.Common.dll:TestInventoryService");
            config.Configs["InventoryService"].Set("StorageProvider", "OpenSim.Tests.Common.dll");
            inventoryService.Initialise(config);
            inventoryService.AddRegion(testScene);
            inventoryService.RegionLoaded(testScene);
            testScene.AddRegionModule(inventoryService.Name, inventoryService);
            m_inventoryService = inventoryService;
        }

        private static void StartGridService(Scene testScene, bool real)
        {
            IConfigSource config = new IniConfigSource();
            config.AddConfig("Modules");
            config.AddConfig("GridService");
            config.Configs["Modules"].Set("GridServices", "LocalGridServicesConnector");
            config.Configs["GridService"].Set("StorageProvider", "OpenSim.Data.Null.dll:NullRegionData");
            if (real)
                config.Configs["GridService"].Set("LocalServiceModule", "OpenSim.Services.GridService.dll:GridService");
            if (m_gridService == null)
            {
                ISharedRegionModule gridService = new LocalGridServicesConnector();
                gridService.Initialise(config);
                m_gridService = gridService;
            }
            //else
            //    config.Configs["GridService"].Set("LocalServiceModule", "OpenSim.Tests.Common.dll:TestGridService");
            m_gridService.AddRegion(testScene);
            m_gridService.RegionLoaded(testScene);
            //testScene.AddRegionModule(m_gridService.Name, m_gridService);
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
                module.RegionLoaded(scene);
                scene.AddRegionModule(module.Name, module);
            }

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

            return agentData;
        }

        /// <summary>
        /// Add a root agent where the details of the agent connection (apart from the id) are unimportant for the test
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="agentId"></param>
        /// <returns></returns>
        public static TestClient AddRootAgent(Scene scene, UUID agentId)
        {
            return AddRootAgent(scene, GenerateAgentData(agentId));
        }

        /// <summary>
        /// Add a root agent.
        /// </summary>
        ///
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
        ///
        /// <param name="scene"></param>
        /// <param name="agentData"></param>
        /// <returns></returns>
        public static TestClient AddRootAgent(Scene scene, AgentCircuitData agentData)
        {
            string reason;

            // We emulate the proper login sequence here by doing things in three stages
            // Stage 1: simulate login by telling the scene to expect a new user connection
            scene.NewUserConnection(agentData, out reason);

            // Stage 2: add the new client as a child agent to the scene
            TestClient client = new TestClient(agentData, scene);
            scene.AddNewClient(client);

            // Stage 3: Invoke agent crossing, which converts the child agent into a root agent (with appearance,
            // inventory, etc.)
            //scene.AgentCrossing(agentData.AgentID, new Vector3(90, 90, 90), false); OBSOLETE

            ScenePresence scp = scene.GetScenePresence(agentData.AgentID);
            scp.MakeRootAgent(new Vector3(90,90,90), true);

            return client;
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
            SceneObjectPart part
                = new SceneObjectPart(UUID.Zero, PrimitiveBaseShape.Default, Vector3.Zero, Quaternion.Identity, Vector3.Zero);
            part.Name = name;

            //part.UpdatePrimFlags(false, false, true);
            //part.ObjectFlags |= (uint)PrimFlags.Phantom;

            scene.AddNewSceneObject(new SceneObjectGroup(part), false);

            return part;
        }

        /// <summary>
        /// Delete a scene object asynchronously
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="part"></param>
        /// <param name="action"></param>
        /// <param name="destinationId"></param>
        /// <param name="client"></param>
        public static void DeleteSceneObjectAsync(
            TestScene scene, SceneObjectPart part, DeRezAction action, UUID destinationId, IClientAPI client)
        {
            // Turn off the timer on the async sog deleter - we'll crank it by hand within a unit test
            AsyncSceneObjectGroupDeleter sogd = scene.SceneObjectGroupDeleter;
            sogd.Enabled = false;

            scene.DeRezObject(client, part.LocalId, UUID.Zero, action, destinationId);
            sogd.InventoryDeQueueAndDelete();
        }
    }
    
}
