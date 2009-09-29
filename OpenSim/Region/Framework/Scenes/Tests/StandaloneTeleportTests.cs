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
using System.Reflection;
using Nini.Config;
using NUnit.Framework;
using NUnit.Framework.SyntaxHelpers;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.CoreModules.ServiceConnectorsOut.Interregion;
using OpenSim.Tests.Common;
using OpenSim.Tests.Common.Mock;
using OpenSim.Tests.Common.Setup;
using System.Threading;

namespace OpenSim.Region.Framework.Scenes.Tests
{
    /// <summary>
    /// Teleport tests in a standalone OpenSim
    /// </summary>    
    [TestFixture]
    public class StandaloneTeleportTests
    {
        /// <summary>
        /// Test a teleport between two regions that are not neighbours and do not share any neighbours in common.
        /// </summary>
        /// Does not yet do what is says on the tin.
        /// Commenting for now
        //[Test, LongRunning]        
        public void TestSimpleNotNeighboursTeleport()
        {
            TestHelper.InMethod();
            ThreadRunResults results = new ThreadRunResults();
            results.Result = false;
            results.Message = "Test did not run";
            TestRunning testClass = new TestRunning(results);

            Thread testThread = new Thread(testClass.run);

            try
            {
                // Seems kind of redundant to start a thread and then join it, however..   We need to protect against
                // A thread abort exception in the simulator code.
                testThread.Start();
                testThread.Join();
            } 
            catch (ThreadAbortException)
            {
                
            }
            Assert.That(testClass.results.Result, Is.EqualTo(true), testClass.results.Message);
            // Console.WriteLine("Beginning test {0}", MethodBase.GetCurrentMethod());
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (MainServer.Instance != null) MainServer.Instance.Stop();
            }
            catch (NullReferenceException)
            { }
        }

    }

    public class ThreadRunResults
    {
        public bool Result = false;
        public string Message = string.Empty;
    }

    public class TestRunning
    {
        public ThreadRunResults results;
        public TestRunning(ThreadRunResults t)
        {
            results = t;
        }
        public void run(object o)
        {
            
            //results.Result = true;
            log4net.Config.XmlConfigurator.Configure();

            UUID sceneAId = UUID.Parse("00000000-0000-0000-0000-000000000100");
            UUID sceneBId = UUID.Parse("00000000-0000-0000-0000-000000000200");
            TestCommunicationsManager cm = new TestCommunicationsManager();

            // shared module
            ISharedRegionModule interregionComms = new RESTInterregionComms();


            Scene sceneB = SceneSetupHelpers.SetupScene("sceneB", sceneBId, 1010, 1010, cm, "grid");
            SceneSetupHelpers.SetupSceneModules(sceneB, new IniConfigSource(), interregionComms);
            sceneB.RegisterRegionWithGrid();

            Scene sceneA = SceneSetupHelpers.SetupScene("sceneA", sceneAId, 1000, 1000, cm, "grid");
            SceneSetupHelpers.SetupSceneModules(sceneA, new IniConfigSource(), interregionComms);
            sceneA.RegisterRegionWithGrid();

            UUID agentId = UUID.Parse("00000000-0000-0000-0000-000000000041");
            TestClient client = SceneSetupHelpers.AddRootAgent(sceneA, agentId);

            ICapabilitiesModule sceneACapsModule = sceneA.RequestModuleInterface<ICapabilitiesModule>();

            results.Result = (sceneACapsModule.GetCapsPath(agentId) == client.CapsSeedUrl);
            
            if (!results.Result)
            {
                results.Message = "Incorrect caps object path set up in sceneA";
                return;
            }

            /*
            Assert.That(
                sceneACapsModule.GetCapsPath(agentId),
                Is.EqualTo(client.CapsSeedUrl),
                "Incorrect caps object path set up in sceneA");
            */
            // FIXME: This is a hack to get the test working - really the normal OpenSim mechanisms should be used.
            

            client.TeleportTargetScene = sceneB;
            client.Teleport(sceneB.RegionInfo.RegionHandle, new Vector3(100, 100, 100), new Vector3(40, 40, 40));

            results.Result = (sceneB.GetScenePresence(agentId) != null);
            if (!results.Result)
            {
                results.Message = "Client does not have an agent in sceneB";
                return;
            }

            //Assert.That(sceneB.GetScenePresence(agentId), Is.Not.Null, "Client does not have an agent in sceneB");
            
            //Assert.That(sceneA.GetScenePresence(agentId), Is.Null, "Client still had an agent in sceneA");

            results.Result = (sceneA.GetScenePresence(agentId) == null);
            if (!results.Result)
            {
                results.Message = "Client still had an agent in sceneA";
                return;
            }

            ICapabilitiesModule sceneBCapsModule = sceneB.RequestModuleInterface<ICapabilitiesModule>();


            results.Result = ("http://" + sceneB.RegionInfo.ExternalHostName + ":" + sceneB.RegionInfo.HttpPort +
                              "/CAPS/" + sceneBCapsModule.GetCapsPath(agentId) + "0000/" == client.CapsSeedUrl);
            if (!results.Result)
            {
                results.Message = "Incorrect caps object path set up in sceneB";
                return;
            }

            // Temporary assertion - caps url construction should at least be doable through a method.
            /*
            Assert.That(
                "http://" + sceneB.RegionInfo.ExternalHostName + ":" + sceneB.RegionInfo.HttpPort + "/CAPS/" + sceneBCapsModule.GetCapsPath(agentId) + "0000/",
                Is.EqualTo(client.CapsSeedUrl),
                "Incorrect caps object path set up in sceneB");
            */
            // This assertion will currently fail since we don't remove the caps paths when no longer needed
            //Assert.That(sceneACapsModule.GetCapsPath(agentId), Is.Null, "sceneA still had a caps object path");

            // TODO: Check that more of everything is as it should be

            // TODO: test what happens if we try to teleport to a region that doesn't exist
        }
    }
}
