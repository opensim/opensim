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
using System.Collections.Generic;
using System.Net;
using Mono.Addins;
using Nini.Config;
using NUnit.Framework;
using OpenMetaverse;
using OpenSim;
using OpenSim.ApplicationPlugins.RegionModulesController;
using OpenSim.Framework;
using OpenSim.Region.CoreModules.ServiceConnectorsOut.Grid;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Tests.Common;

namespace OpenSim.Region.Framework.Scenes.Tests
{
    public class SharedRegionModuleTests : OpenSimTestCase
    {
//        [Test]
        public void TestLifecycle()
        {
            TestHelpers.InMethod();
            TestHelpers.EnableLogging();

            UUID estateOwnerId = TestHelpers.ParseTail(0x1);
            UUID regionId = TestHelpers.ParseTail(0x10);

            IConfigSource configSource = new IniConfigSource();
            configSource.AddConfig("Startup");
            configSource.AddConfig("Modules");

//            // We use this to skip estate questions
            // Turns out not to be needed is estate owner id is pre-set in region information.
//            IConfig estateConfig = configSource.AddConfig(OpenSimBase.ESTATE_SECTION_NAME);
//            estateConfig.Set("DefaultEstateOwnerName", "Zaphod Beeblebrox");
//            estateConfig.Set("DefaultEstateOwnerUUID", estateOwnerId);
//            estateConfig.Set("DefaultEstateOwnerEMail", "zaphod@galaxy.com");
//            estateConfig.Set("DefaultEstateOwnerPassword", "two heads");

            // For grid servic
            configSource.AddConfig("GridService");
            configSource.Configs["Modules"].Set("GridServices", "LocalGridServicesConnector");
            configSource.Configs["GridService"].Set("StorageProvider", "OpenSim.Data.Null.dll:NullRegionData");
            configSource.Configs["GridService"].Set("LocalServiceModule", "OpenSim.Services.GridService.dll:GridService");
            configSource.Configs["GridService"].Set("ConnectionString", "!static");

            LocalGridServicesConnector gridService = new LocalGridServicesConnector();
//
            OpenSim sim = new OpenSim(configSource);

            sim.SuppressExit = true;
            sim.EnableInitialPluginLoad = false;
            sim.LoadEstateDataService = false;
            sim.NetServersInfo.HttpListenerPort = 0;

            IRegistryCore reg = sim.ApplicationRegistry;

            RegionInfo ri = new RegionInfo();
            ri.RegionID = regionId;
            ri.EstateSettings.EstateOwner = estateOwnerId;
            ri.InternalEndPoint = new IPEndPoint(0, 0);

            MockRegionModulesControllerPlugin rmcp = new MockRegionModulesControllerPlugin();
            sim.m_plugins = new List<IApplicationPlugin>() { rmcp };
            reg.RegisterInterface<IRegionModulesController>(rmcp);

            // XXX: Have to initialize directly for now
            rmcp.Initialise(sim);

            rmcp.AddNode(gridService);

            TestSharedRegion tsr = new TestSharedRegion();
            rmcp.AddNode(tsr);

            // FIXME: Want to use the real one eventually but this is currently directly tied into Mono.Addins
            // which has been written in such a way that makes it impossible to use for regression tests.
//            RegionModulesControllerPlugin rmcp = new RegionModulesControllerPlugin();
//            rmcp.LoadModulesFromAddins = false;
////            reg.RegisterInterface<IRegionModulesController>(rmcp);
//            rmcp.Initialise(sim);
//            rmcp.PostInitialise();           
//            TypeExtensionNode node = new TypeExtensionNode();
//            node.
//            rmcp.AddNode(node, configSource.Configs["Modules"], new Dictionary<RuntimeAddin, IList<int>>());

            sim.Startup();
            IScene scene;
            sim.CreateRegion(ri, out scene);

            sim.Shutdown();

            List<string> co = tsr.CallOrder;
            int expectedEventCount = 6;

            Assert.AreEqual(
                expectedEventCount, 
                co.Count, 
                "Expected {0} events but only got {1} ({2})", 
                expectedEventCount, co.Count, string.Join(",", co));
            Assert.AreEqual("Initialise",       co[0]);
            Assert.AreEqual("PostInitialise",   co[1]);
            Assert.AreEqual("AddRegion",        co[2]);
            Assert.AreEqual("RegionLoaded",     co[3]);
            Assert.AreEqual("RemoveRegion",     co[4]);
            Assert.AreEqual("Close",            co[5]);
        }
    }

    class TestSharedRegion : ISharedRegionModule
    {
        // FIXME: Should really use MethodInfo
        public List<string> CallOrder = new List<string>();
                
        public string Name { get { return "TestSharedRegion"; } }

        public Type ReplaceableInterface { get { return null; } }

        public void PostInitialise()
        {
            CallOrder.Add("PostInitialise");
        }

        public void Initialise(IConfigSource source)
        {
            CallOrder.Add("Initialise");
        }

        public void Close()
        {
            CallOrder.Add("Close");
        }

        public void AddRegion(Scene scene)
        {
            CallOrder.Add("AddRegion");
        }

        public void RemoveRegion(Scene scene)
        {
            CallOrder.Add("RemoveRegion");
        }

        public void RegionLoaded(Scene scene)
        {
            CallOrder.Add("RegionLoaded");
        }
    }

    class MockRegionModulesControllerPlugin : IRegionModulesController, IApplicationPlugin
    {
        // List of shared module instances, for adding to Scenes
        private List<ISharedRegionModule> m_sharedInstances = new List<ISharedRegionModule>();

        // Config access
        private OpenSimBase m_openSim;

        public string Version { get { return "0"; } }
        public string Name { get { return "MockRegionModulesControllerPlugin"; } }

        public void Initialise() {}

        public void Initialise(OpenSimBase sim) 
        {
            m_openSim = sim;
        }

        /// <summary>
        /// Called when the application loading is completed 
        /// </summary>
        public void PostInitialise()
        {
            foreach (ISharedRegionModule module in m_sharedInstances)
                module.PostInitialise();
        }

        public void AddRegionToModules(Scene scene)
        {
            List<ISharedRegionModule> sharedlist = new List<ISharedRegionModule>();

            foreach (ISharedRegionModule module in m_sharedInstances)
            {
                module.AddRegion(scene);
                scene.AddRegionModule(module.Name, module);

                sharedlist.Add(module);
            }

            foreach (ISharedRegionModule module in sharedlist)
            {
                module.RegionLoaded(scene);
            }
        }

        public void RemoveRegionFromModules(Scene scene)
        {
            foreach (IRegionModuleBase module in scene.RegionModules.Values)
            {
//                m_log.DebugFormat("[REGIONMODULE]: Removing scene {0} from module {1}",
//                                  scene.RegionInfo.RegionName, module.Name);
                module.RemoveRegion(scene);
            }

            scene.RegionModules.Clear();
        }       
        
        public void AddNode(ISharedRegionModule module)
        {
            m_sharedInstances.Add(module);
            module.Initialise(m_openSim.ConfigSource.Source);
        }

        public void Dispose()
        {
            // We expect that all regions have been removed already
            while (m_sharedInstances.Count > 0)
            {
                m_sharedInstances[0].Close();
                m_sharedInstances.RemoveAt(0);
            }
        }
    }
}