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
using log4net.Config;
using Nini.Config;
using NUnit.Framework;
using OpenMetaverse;
using OpenMetaverse.Packets;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.ClientStack.Linden;
using OpenSim.Region.CoreModules.Framework;
using OpenSim.Tests.Common;
using OpenSim.Tests.Common.Mock;

namespace OpenSim.Region.ClientStack.Linden.Tests
{
    [TestFixture]
    public class EventQueueTests : OpenSimTestCase
    {
        private TestScene m_scene;

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

            IConfigSource config = new IniConfigSource();
            config.AddConfig("Startup");
            config.Configs["Startup"].Set("EventQueue", "true");

            CapabilitiesModule capsModule = new CapabilitiesModule();
            EventQueueGetModule eqgModule = new EventQueueGetModule();

            m_scene = new SceneHelpers().SetupScene();
            SceneHelpers.SetupSceneModules(m_scene, config, capsModule, eqgModule);
        }

        [Test]
        public void TestAddForClient()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            SceneHelpers.AddScenePresence(m_scene, TestHelpers.ParseTail(0x1));

            // TODO: Add more assertions for the other aspects of event queues
            Assert.That(MainServer.Instance.GetPollServiceHandlerKeys().Count, Is.EqualTo(1));
        }

        [Test]
        public void TestRemoveForClient()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            UUID spId = TestHelpers.ParseTail(0x1);

            SceneHelpers.AddScenePresence(m_scene, spId);
            m_scene.IncomingCloseAgent(spId, false);

            // TODO: Add more assertions for the other aspects of event queues
            Assert.That(MainServer.Instance.GetPollServiceHandlerKeys().Count, Is.EqualTo(0));
        }
    }
}