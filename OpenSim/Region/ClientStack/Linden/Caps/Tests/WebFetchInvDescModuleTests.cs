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
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using HttpServer;
using log4net.Config;
using Nini.Config;
using NUnit.Framework;
using OpenMetaverse;
using OpenMetaverse.Packets;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Framework.Capabilities;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.ClientStack.Linden;
using OpenSim.Region.CoreModules.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using OpenSim.Tests.Common;
using OSDArray = OpenMetaverse.StructuredData.OSDArray;
using OSDMap = OpenMetaverse.StructuredData.OSDMap;

namespace OpenSim.Region.ClientStack.Linden.Caps.Tests
{
    [TestFixture]
    public class WebFetchInvDescModuleTests : OpenSimTestCase
    {
        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {
            // Don't allow tests to be bamboozled by asynchronous events.  Execute everything on the same thread.
            Util.FireAndForgetMethod = FireAndForgetMethod.RegressionTest;
        }

        [TestFixtureTearDown]
        public void TestFixureTearDown()
        {
            // We must set this back afterwards, otherwise later tests will fail since they're expecting multiple
            // threads.  Possibly, later tests should be rewritten so none of them require async stuff (which regression
            // tests really shouldn't).
            Util.FireAndForgetMethod = Util.DefaultFireAndForgetMethod;
        }

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            // This is an unfortunate bit of clean up we have to do because MainServer manages things through static
            // variables and the VM is not restarted between tests.
            uint port = 9999;
            MainServer.RemoveHttpServer(port);

            BaseHttpServer server = new BaseHttpServer(port, false, 0, "");
            MainServer.AddHttpServer(server);
            MainServer.Instance = server;

            server.Start(false);
        }

        [Test]
        public void TestInventoryDescendentsFetch()
        {
            TestHelpers.InMethod();
            TestHelpers.EnableLogging();

            BaseHttpServer httpServer = MainServer.Instance;
            Scene scene = new SceneHelpers().SetupScene();

            CapabilitiesModule capsModule = new CapabilitiesModule();
            WebFetchInvDescModule wfidModule = new WebFetchInvDescModule(false);

            IConfigSource config = new IniConfigSource();
            config.AddConfig("ClientStack.LindenCaps");
            config.Configs["ClientStack.LindenCaps"].Set("Cap_FetchInventoryDescendents2", "localhost");

            SceneHelpers.SetupSceneModules(scene, config, capsModule, wfidModule);

            UserAccount ua = UserAccountHelpers.CreateUserWithInventory(scene, TestHelpers.ParseTail(0x1));

            // We need a user present to have any capabilities set up
            SceneHelpers.AddScenePresence(scene, ua.PrincipalID);

            TestHttpRequest req = new TestHttpRequest();
            OpenSim.Framework.Capabilities.Caps userCaps = capsModule.GetCapsForUser(ua.PrincipalID);
            PollServiceEventArgs pseArgs;
            userCaps.TryGetPollHandler("FetchInventoryDescendents2", out pseArgs);
            req.UriPath = pseArgs.Url;
            req.Uri = new Uri(req.UriPath);

            // Retrieve root folder details directly so that we can request
            InventoryFolderBase folder = scene.InventoryService.GetRootFolder(ua.PrincipalID);

            OSDMap osdFolder = new OSDMap();
            osdFolder["folder_id"] = folder.ID;
            osdFolder["owner_id"] = ua.PrincipalID;
            osdFolder["fetch_folders"] = true;
            osdFolder["fetch_items"] = true;
            osdFolder["sort_order"] = 0;

            OSDArray osdFoldersArray = new OSDArray();
            osdFoldersArray.Add(osdFolder);

            OSDMap osdReqMap = new OSDMap();
            osdReqMap["folders"] = osdFoldersArray;

            req.Body = new MemoryStream(OSDParser.SerializeLLSDXmlBytes(osdReqMap));

            TestHttpClientContext context = new TestHttpClientContext(false);
            MainServer.Instance.OnRequest(context, new RequestEventArgs(req));

            // Drive processing of the queued inventory request synchronously.
            wfidModule.WaitProcessQueuedInventoryRequest();
            MainServer.Instance.PollServiceRequestManager.WaitPerformResponse();

//            System.Threading.Thread.Sleep(10000);

            OSDMap responseOsd = (OSDMap)OSDParser.DeserializeLLSDXml(context.ResponseBody);
            OSDArray foldersOsd = (OSDArray)responseOsd["folders"];
            OSDMap folderOsd = (OSDMap)foldersOsd[0];
           
            // A sanity check that the response has the expected number of descendents for a default inventory
            // TODO: Need a more thorough check.
            Assert.That((int)folderOsd["descendents"], Is.EqualTo(16));
        }
    }
}