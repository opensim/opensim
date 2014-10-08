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
using log4net.Config;
using Nini.Config;
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.CoreModules.Avatar.Chat;
using OpenSim.Region.CoreModules.Framework;
using OpenSim.Region.CoreModules.Framework.EntityTransfer;
using OpenSim.Region.CoreModules.ServiceConnectorsOut.Simulation;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using OpenSim.Tests.Common;

namespace OpenSim.Region.CoreModules.Avatar.Chat.Tests
{
    [TestFixture]
    public class ChatModuleTests : OpenSimTestCase
    {  
        [TestFixtureSetUp]
        public void FixtureInit()
        {
            // Don't allow tests to be bamboozled by asynchronous events.  Execute everything on the same thread.
            // We must do this here so that child agent positions are updated in a predictable manner.
            Util.FireAndForgetMethod = FireAndForgetMethod.RegressionTest;
        }

        [TestFixtureTearDown]
        public void TearDown()
        {
            // We must set this back afterwards, otherwise later tests will fail since they're expecting multiple
            // threads.  Possibly, later tests should be rewritten so none of them require async stuff (which regression
            // tests really shouldn't).
            Util.FireAndForgetMethod = Util.DefaultFireAndForgetMethod;
        }

        private void SetupNeighbourRegions(TestScene sceneA, TestScene sceneB)
        {            
            // XXX: HTTP server is not (and should not be) necessary for this test, though it's absence makes the 
            // CapabilitiesModule complain when it can't set up HTTP endpoints.
            //            BaseHttpServer httpServer = new BaseHttpServer(99999);
            //            MainServer.AddHttpServer(httpServer);
            //            MainServer.Instance = httpServer;

            // We need entity transfer modules so that when sp2 logs into the east region, the region calls 
            // EntityTransferModuleto set up a child agent on the west region.
            // XXX: However, this is not an entity transfer so is misleading.
            EntityTransferModule etmA = new EntityTransferModule();
            EntityTransferModule etmB = new EntityTransferModule();
            LocalSimulationConnectorModule lscm = new LocalSimulationConnectorModule();

            IConfigSource config = new IniConfigSource();
            config.AddConfig("Chat");
            IConfig modulesConfig = config.AddConfig("Modules");
            modulesConfig.Set("EntityTransferModule", etmA.Name);
            modulesConfig.Set("SimulationServices", lscm.Name);

            SceneHelpers.SetupSceneModules(new Scene[] { sceneA, sceneB }, config, lscm);
            SceneHelpers.SetupSceneModules(sceneA, config, new CapabilitiesModule(), etmA, new ChatModule());           
            SceneHelpers.SetupSceneModules(sceneB, config, new CapabilitiesModule(), etmB, new ChatModule());
        }

        /// <summary>
        /// Tests chat between neighbour regions on the east-west axis
        /// </summary>
        /// <remarks>
        /// Really, this is a combination of a child agent position update test and a chat range test.  These need
        /// to be separated later on.
        /// </remarks>
        [Test]
        public void TestInterRegionChatDistanceEastWest()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            UUID sp1Uuid = TestHelpers.ParseTail(0x11);
            UUID sp2Uuid = TestHelpers.ParseTail(0x12);

            Vector3 sp1Position = new Vector3(6, 128, 20);
            Vector3 sp2Position = new Vector3(250, 128, 20);

            SceneHelpers sh = new SceneHelpers();
            TestScene sceneWest = sh.SetupScene("sceneWest", TestHelpers.ParseTail(0x1), 1000, 1000);            
            TestScene sceneEast = sh.SetupScene("sceneEast", TestHelpers.ParseTail(0x2), 1001, 1000);            

            SetupNeighbourRegions(sceneWest, sceneEast);

            ScenePresence sp1 = SceneHelpers.AddScenePresence(sceneEast, sp1Uuid);
            TestClient sp1Client = (TestClient)sp1.ControllingClient;

            // If we don't set agents to flying, test will go wrong as they instantly fall to z = 0.
            // TODO: May need to create special complete no-op test physics module rather than basic physics, since
            // physics is irrelevant to this test.
            sp1.Flying = true;

            // When sp1 logs in to sceneEast, it sets up a child agent in sceneWest and informs the sp2 client to 
            // make the connection.  For this test, will simplify this chain by making the connection directly.
            ScenePresence sp1Child = SceneHelpers.AddChildScenePresence(sceneWest, sp1Uuid);
            TestClient sp1ChildClient = (TestClient)sp1Child.ControllingClient;

            sp1.AbsolutePosition = sp1Position;                

            ScenePresence sp2 = SceneHelpers.AddScenePresence(sceneWest, sp2Uuid);
            TestClient sp2Client = (TestClient)sp2.ControllingClient;
            sp2.Flying = true;

            ScenePresence sp2Child = SceneHelpers.AddChildScenePresence(sceneEast, sp2Uuid);
            TestClient sp2ChildClient = (TestClient)sp2Child.ControllingClient;

            sp2.AbsolutePosition = sp2Position;           

            // We must update the scenes in order to make the root new root agents trigger position updates in their
            // children.
            sceneWest.Update(1);
            sceneEast.Update(1);

            // Check child positions are correct.
            Assert.AreEqual(
                new Vector3(sp1Position.X + sceneEast.RegionInfo.RegionSizeX, sp1Position.Y, sp1Position.Z), 
                sp1ChildClient.SceneAgent.AbsolutePosition);

            Assert.AreEqual(
                new Vector3(sp2Position.X - sceneWest.RegionInfo.RegionSizeX, sp2Position.Y, sp2Position.Z), 
                sp2ChildClient.SceneAgent.AbsolutePosition);

            string receivedSp1ChatMessage = "";
            string receivedSp2ChatMessage = "";

            sp1ChildClient.OnReceivedChatMessage 
                += (message, type, fromPos, fromName, fromAgentID, ownerID, source, audible) => receivedSp1ChatMessage = message;
            sp2ChildClient.OnReceivedChatMessage 
                += (message, type, fromPos, fromName, fromAgentID, ownerID, source, audible) => receivedSp2ChatMessage = message;

            TestUserInRange(sp1Client, "ello darling", ref receivedSp2ChatMessage);
            TestUserInRange(sp2Client, "fantastic cats", ref receivedSp1ChatMessage);

            sp1Position = new Vector3(30, 128, 20);
            sp1.AbsolutePosition = sp1Position;
            sceneEast.Update(1);

            // Check child position is correct.
            Assert.AreEqual(
                new Vector3(sp1Position.X + sceneEast.RegionInfo.RegionSizeX, sp1Position.Y, sp1Position.Z), 
                sp1ChildClient.SceneAgent.AbsolutePosition);

            TestUserOutOfRange(sp1Client, "beef", ref receivedSp2ChatMessage);
            TestUserOutOfRange(sp2Client, "lentils", ref receivedSp1ChatMessage);
        }

        /// <summary>
        /// Tests chat between neighbour regions on the north-south axis
        /// </summary>
        /// <remarks>
        /// Really, this is a combination of a child agent position update test and a chat range test.  These need
        /// to be separated later on.
        /// </remarks>
        [Test]
        public void TestInterRegionChatDistanceNorthSouth()
        {
            TestHelpers.InMethod();
            //            TestHelpers.EnableLogging();

            UUID sp1Uuid = TestHelpers.ParseTail(0x11);
            UUID sp2Uuid = TestHelpers.ParseTail(0x12);

            Vector3 sp1Position = new Vector3(128, 250, 20);
            Vector3 sp2Position = new Vector3(128, 6, 20);

            SceneHelpers sh = new SceneHelpers();
            TestScene sceneNorth = sh.SetupScene("sceneNorth", TestHelpers.ParseTail(0x1), 1000, 1000);            
            TestScene sceneSouth = sh.SetupScene("sceneSouth", TestHelpers.ParseTail(0x2), 1000, 1001);            

            SetupNeighbourRegions(sceneNorth, sceneSouth);

            ScenePresence sp1 = SceneHelpers.AddScenePresence(sceneNorth, sp1Uuid);
            TestClient sp1Client = (TestClient)sp1.ControllingClient;

            // If we don't set agents to flying, test will go wrong as they instantly fall to z = 0.
            // TODO: May need to create special complete no-op test physics module rather than basic physics, since
            // physics is irrelevant to this test.
            sp1.Flying = true;

            // When sp1 logs in to sceneEast, it sets up a child agent in sceneNorth and informs the sp2 client to 
            // make the connection.  For this test, will simplify this chain by making the connection directly.
            ScenePresence sp1Child = SceneHelpers.AddChildScenePresence(sceneSouth, sp1Uuid);
            TestClient sp1ChildClient = (TestClient)sp1Child.ControllingClient;

            sp1.AbsolutePosition = sp1Position;                

            ScenePresence sp2 = SceneHelpers.AddScenePresence(sceneSouth, sp2Uuid);
            TestClient sp2Client = (TestClient)sp2.ControllingClient;
            sp2.Flying = true;

            ScenePresence sp2Child = SceneHelpers.AddChildScenePresence(sceneNorth, sp2Uuid);
            TestClient sp2ChildClient = (TestClient)sp2Child.ControllingClient;

            sp2.AbsolutePosition = sp2Position;           

            // We must update the scenes in order to make the root new root agents trigger position updates in their
            // children.
            sceneNorth.Update(1);
            sceneSouth.Update(1);

            // Check child positions are correct.
            Assert.AreEqual(
                new Vector3(sp1Position.X, sp1Position.Y - sceneNorth.RegionInfo.RegionSizeY, sp1Position.Z), 
                sp1ChildClient.SceneAgent.AbsolutePosition);

            Assert.AreEqual(
                new Vector3(sp2Position.X, sp2Position.Y + sceneSouth.RegionInfo.RegionSizeY, sp2Position.Z), 
                sp2ChildClient.SceneAgent.AbsolutePosition);

            string receivedSp1ChatMessage = "";
            string receivedSp2ChatMessage = "";

            sp1ChildClient.OnReceivedChatMessage 
                += (message, type, fromPos, fromName, fromAgentID, ownerID, source, audible) => receivedSp1ChatMessage = message;
            sp2ChildClient.OnReceivedChatMessage 
                += (message, type, fromPos, fromName, fromAgentID, ownerID, source, audible) => receivedSp2ChatMessage = message;

            TestUserInRange(sp1Client, "ello darling", ref receivedSp2ChatMessage);
            TestUserInRange(sp2Client, "fantastic cats", ref receivedSp1ChatMessage);

            sp1Position = new Vector3(30, 128, 20);
            sp1.AbsolutePosition = sp1Position;
            sceneNorth.Update(1);

            // Check child position is correct.
            Assert.AreEqual(
                new Vector3(sp1Position.X, sp1Position.Y - sceneNorth.RegionInfo.RegionSizeY, sp1Position.Z), 
                sp1ChildClient.SceneAgent.AbsolutePosition);

            TestUserOutOfRange(sp1Client, "beef", ref receivedSp2ChatMessage);
            TestUserOutOfRange(sp2Client, "lentils", ref receivedSp1ChatMessage);
        }
    
        private void TestUserInRange(TestClient speakClient, string testMessage, ref string receivedMessage)
        {
            receivedMessage = "";

            speakClient.Chat(0, ChatTypeEnum.Say, testMessage);

            Assert.AreEqual(testMessage, receivedMessage);
        }

        private void TestUserOutOfRange(TestClient speakClient, string testMessage, ref string receivedMessage)
        {
            receivedMessage = "";

            speakClient.Chat(0, ChatTypeEnum.Say, testMessage);

            Assert.AreNotEqual(testMessage, receivedMessage);
        }
    }
}