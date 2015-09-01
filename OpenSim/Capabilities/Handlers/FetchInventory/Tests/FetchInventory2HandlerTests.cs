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
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using log4net;
using log4net.Config;
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Capabilities.Handlers;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using OpenSim.Tests.Common;

namespace OpenSim.Capabilities.Handlers.FetchInventory.Tests
{
    [TestFixture]
    public class FetchInventory2HandlerTests : OpenSimTestCase
    {
        private UUID m_userID = UUID.Random();
        private Scene m_scene;
        private UUID m_rootFolderID;
        private UUID m_notecardsFolder;
        private UUID m_objectsFolder;

        private void Init()
        {
            // Create an inventory that looks like this:
            //
            // /My Inventory
            //   <other system folders>
            //   /Objects
            //      Object 1
            //      Object 2
            //      Object 3
            //   /Notecards
            //      Notecard 1
            //      Notecard 2
            //      Notecard 3
            //      Notecard 4
            //      Notecard 5

            m_scene = new SceneHelpers().SetupScene();

            m_scene.InventoryService.CreateUserInventory(m_userID);

            m_rootFolderID = m_scene.InventoryService.GetRootFolder(m_userID).ID;

            InventoryFolderBase of = m_scene.InventoryService.GetFolderForType(m_userID, FolderType.Object);
            m_objectsFolder = of.ID;

            // Add 3 objects
            InventoryItemBase item;
            for (int i = 1; i <= 3; i++)
            {
                item = new InventoryItemBase(new UUID("b0000000-0000-0000-0000-0000000000b" + i), m_userID);
                item.AssetID = UUID.Random();
                item.AssetType = (int)AssetType.Object;
                item.Folder = m_objectsFolder;
                item.Name = "Object " + i;
                m_scene.InventoryService.AddItem(item);
            }

            InventoryFolderBase ncf = m_scene.InventoryService.GetFolderForType(m_userID, FolderType.Notecard);
            m_notecardsFolder = ncf.ID;

            // Add 5 notecards
            for (int i = 1; i <= 5; i++)
            {
                item = new InventoryItemBase(new UUID("10000000-0000-0000-0000-00000000000" + i), m_userID);
                item.AssetID = UUID.Random();
                item.AssetType = (int)AssetType.Notecard;
                item.Folder = m_notecardsFolder;
                item.Name = "Notecard " + i;
                m_scene.InventoryService.AddItem(item);
            }

        }

        [Test]
        public void Test_001_RequestOne()
        {
            TestHelpers.InMethod();

            Init();

            FetchInventory2Handler handler = new FetchInventory2Handler(m_scene.InventoryService, m_userID);
            TestOSHttpRequest req = new TestOSHttpRequest();
            TestOSHttpResponse resp = new TestOSHttpResponse();

            string request = "<llsd><map><key>items</key><array><map><key>item_id</key><uuid>";
            request += "10000000-0000-0000-0000-000000000001"; // Notecard 1
            request += "</uuid></map></array></map></llsd>";
            
            string llsdresponse = handler.FetchInventoryRequest(request, "/FETCH", string.Empty, req, resp);

            Assert.That(llsdresponse != null, Is.True, "Incorrect null response");
            Assert.That(llsdresponse != string.Empty, Is.True, "Incorrect empty response");
            Assert.That(llsdresponse.Contains(m_userID.ToString()), Is.True, "Response should contain userID");

            Assert.That(llsdresponse.Contains("10000000-0000-0000-0000-000000000001"), Is.True, "Response does not contain item uuid");
            Assert.That(llsdresponse.Contains("Notecard 1"), Is.True, "Response does not contain item Name");
            Console.WriteLine(llsdresponse);
        }

        [Test]
        public void Test_002_RequestMany()
        {
            TestHelpers.InMethod();

            Init();

            FetchInventory2Handler handler = new FetchInventory2Handler(m_scene.InventoryService, m_userID);
            TestOSHttpRequest req = new TestOSHttpRequest();
            TestOSHttpResponse resp = new TestOSHttpResponse();

            string request = "<llsd><map><key>items</key><array>";
            request += "<map><key>item_id</key><uuid>10000000-0000-0000-0000-000000000001</uuid></map>"; // Notecard 1
            request += "<map><key>item_id</key><uuid>10000000-0000-0000-0000-000000000002</uuid></map>"; // Notecard 2
            request += "<map><key>item_id</key><uuid>10000000-0000-0000-0000-000000000003</uuid></map>"; // Notecard 3
            request += "<map><key>item_id</key><uuid>10000000-0000-0000-0000-000000000004</uuid></map>"; // Notecard 4
            request += "<map><key>item_id</key><uuid>10000000-0000-0000-0000-000000000005</uuid></map>"; // Notecard 5
            request += "</array></map></llsd>";

            string llsdresponse = handler.FetchInventoryRequest(request, "/FETCH", string.Empty, req, resp);

            Assert.That(llsdresponse != null, Is.True, "Incorrect null response");
            Assert.That(llsdresponse != string.Empty, Is.True, "Incorrect empty response");
            Assert.That(llsdresponse.Contains(m_userID.ToString()), Is.True, "Response should contain userID");

            Console.WriteLine(llsdresponse);
            Assert.That(llsdresponse.Contains("10000000-0000-0000-0000-000000000001"), Is.True, "Response does not contain notecard 1");
            Assert.That(llsdresponse.Contains("10000000-0000-0000-0000-000000000002"), Is.True, "Response does not contain notecard 2");
            Assert.That(llsdresponse.Contains("10000000-0000-0000-0000-000000000003"), Is.True, "Response does not contain notecard 3");
            Assert.That(llsdresponse.Contains("10000000-0000-0000-0000-000000000004"), Is.True, "Response does not contain notecard 4");
            Assert.That(llsdresponse.Contains("10000000-0000-0000-0000-000000000005"), Is.True, "Response does not contain notecard 5");
        }

    }

}