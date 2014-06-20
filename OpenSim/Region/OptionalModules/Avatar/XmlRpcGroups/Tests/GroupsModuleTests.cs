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
using System.Collections;
using System.Net;
using System.Reflection;
using Nini.Config;
using NUnit.Framework;
using OpenMetaverse;
using OpenMetaverse.Messages.Linden;
using OpenMetaverse.Packets;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.ClientStack.Linden;
using OpenSim.Region.CoreModules.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.OptionalModules.Avatar.XmlRpcGroups;
using OpenSim.Tests.Common;
using OpenSim.Tests.Common.Mock;

namespace OpenSim.Region.OptionalModules.Avatar.XmlRpcGroups.Tests
{
    /// <summary>
    /// Basic groups module tests
    /// </summary>
    [TestFixture]
    public class GroupsModuleTests : OpenSimTestCase
    {
        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            uint port = 9999;
            uint sslPort = 9998;

            // This is an unfortunate bit of clean up we have to do because MainServer manages things through static
            // variables and the VM is not restarted between tests.
            MainServer.RemoveHttpServer(port);

            BaseHttpServer server = new BaseHttpServer(port, false, sslPort, "");
            MainServer.AddHttpServer(server);
            MainServer.Instance = server;
        }

        [Test]
        public void TestSendAgentGroupDataUpdate()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();
            
            TestScene scene = new SceneHelpers().SetupScene();
            IConfigSource configSource = new IniConfigSource();
            IConfig config = configSource.AddConfig("Groups");            
            config.Set("Enabled", true);
            config.Set("Module", "GroupsModule");            
            config.Set("DebugEnabled", true);

            GroupsModule gm = new GroupsModule();
            EventQueueGetModule eqgm = new EventQueueGetModule();

            // We need a capabilities module active so that adding the scene presence creates an event queue in the
            // EventQueueGetModule
            SceneHelpers.SetupSceneModules(
                scene, configSource, gm, new MockGroupsServicesConnector(), new CapabilitiesModule(), eqgm);

            ScenePresence sp = SceneHelpers.AddScenePresence(scene, TestHelpers.ParseStem("1"));

            gm.SendAgentGroupDataUpdate(sp.ControllingClient);

            Hashtable eventsResponse = eqgm.GetEvents(UUID.Zero, sp.UUID);

            Assert.That((int)eventsResponse["int_response_code"], Is.EqualTo((int)HttpStatusCode.OK));

//            Console.WriteLine("Response [{0}]", (string)eventsResponse["str_response_string"]);

            OSDMap rawOsd = (OSDMap)OSDParser.DeserializeLLSDXml((string)eventsResponse["str_response_string"]);
            OSDArray eventsOsd = (OSDArray)rawOsd["events"];

            bool foundUpdate = false;
            foreach (OSD osd in eventsOsd)
            {
                OSDMap eventOsd = (OSDMap)osd;

                if (eventOsd["message"] == "AgentGroupDataUpdate")
                    foundUpdate = true;
            }

            Assert.That(foundUpdate, Is.True, "Did not find AgentGroupDataUpdate in response");

            // TODO: More checking of more actual event data.           
        }
    }
}