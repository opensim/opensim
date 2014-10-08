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
using OpenSim.Region.CoreModules.Avatar.InstantMessage;
using OpenSim.Region.CoreModules.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.OptionalModules.Avatar.XmlRpcGroups;
using OpenSim.Tests.Common;

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

        [Test]
        public void TestSendGroupNotice()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();
            
            TestScene scene = new SceneHelpers().SetupScene();

            MessageTransferModule mtm = new MessageTransferModule();
            GroupsModule gm = new GroupsModule();
            GroupsMessagingModule gmm = new GroupsMessagingModule();
            MockGroupsServicesConnector mgsc = new MockGroupsServicesConnector();

            IConfigSource configSource = new IniConfigSource();

            {
                IConfig config = configSource.AddConfig("Messaging");            
                config.Set("MessageTransferModule", mtm.Name);
            }

            {
                IConfig config = configSource.AddConfig("Groups");            
                config.Set("Enabled", true);
                config.Set("Module", gm.Name);
                config.Set("DebugEnabled", true);
                config.Set("MessagingModule", gmm.Name);
                config.Set("MessagingEnabled", true);
            }

            SceneHelpers.SetupSceneModules(scene, configSource, mgsc, mtm, gm, gmm);

            UUID userId = TestHelpers.ParseTail(0x1);
            string subjectText = "newman";
            string messageText = "Hello";
            string combinedSubjectMessage = string.Format("{0}|{1}", subjectText, messageText);

            ScenePresence sp = SceneHelpers.AddScenePresence(scene, TestHelpers.ParseTail(0x1));
            TestClient tc = (TestClient)sp.ControllingClient;

            UUID groupID = gm.CreateGroup(tc, "group1", null, true, UUID.Zero, 0, true, true, true);
            gm.JoinGroupRequest(tc, groupID);

            // Create a second user who doesn't want to receive notices
            ScenePresence sp2 = SceneHelpers.AddScenePresence(scene, TestHelpers.ParseTail(0x2));
            TestClient tc2 = (TestClient)sp2.ControllingClient;
            gm.JoinGroupRequest(tc2, groupID);
            gm.SetGroupAcceptNotices(tc2, groupID, false, true);

            List<GridInstantMessage> spReceivedMessages = new List<GridInstantMessage>();
            tc.OnReceivedInstantMessage += im => spReceivedMessages.Add(im);

            List<GridInstantMessage> sp2ReceivedMessages = new List<GridInstantMessage>();
            tc2.OnReceivedInstantMessage += im => sp2ReceivedMessages.Add(im);

            GridInstantMessage noticeIm = new GridInstantMessage();
            noticeIm.fromAgentID = userId.Guid;
            noticeIm.toAgentID = groupID.Guid;
            noticeIm.message = combinedSubjectMessage;
            noticeIm.dialog = (byte)InstantMessageDialog.GroupNotice;

            tc.HandleImprovedInstantMessage(noticeIm);

            Assert.That(spReceivedMessages.Count, Is.EqualTo(1));
            Assert.That(spReceivedMessages[0].message, Is.EqualTo(combinedSubjectMessage));

            List<GroupNoticeData> notices = mgsc.GetGroupNotices(UUID.Zero, groupID);
            Assert.AreEqual(1, notices.Count);

            // OpenSimulator (possibly also SL) transport the notice ID as the session ID!
            Assert.AreEqual(notices[0].NoticeID.Guid, spReceivedMessages[0].imSessionID);

            Assert.That(sp2ReceivedMessages.Count, Is.EqualTo(0));
        }

        /// <summary>
        /// Run test with the MessageOnlineUsersOnly flag set.
        /// </summary>
        [Test]
        public void TestSendGroupNoticeOnlineOnly()
        {
            TestHelpers.InMethod();
            //            TestHelpers.EnableLogging();

            TestScene scene = new SceneHelpers().SetupScene();

            MessageTransferModule mtm = new MessageTransferModule();
            GroupsModule gm = new GroupsModule();
            GroupsMessagingModule gmm = new GroupsMessagingModule();

            IConfigSource configSource = new IniConfigSource();

            {
                IConfig config = configSource.AddConfig("Messaging");
                config.Set("MessageTransferModule", mtm.Name);
            }

            {
                IConfig config = configSource.AddConfig("Groups");
                config.Set("Enabled", true);
                config.Set("Module", gm.Name);
                config.Set("DebugEnabled", true);
                config.Set("MessagingModule", gmm.Name);
                config.Set("MessagingEnabled", true);
                config.Set("MessageOnlineUsersOnly", true);
            }

            SceneHelpers.SetupSceneModules(scene, configSource, new MockGroupsServicesConnector(), mtm, gm, gmm);

            UUID userId = TestHelpers.ParseTail(0x1);
            string subjectText = "newman";
            string messageText = "Hello";
            string combinedSubjectMessage = string.Format("{0}|{1}", subjectText, messageText);

            ScenePresence sp = SceneHelpers.AddScenePresence(scene, TestHelpers.ParseTail(0x1));
            TestClient tc = (TestClient)sp.ControllingClient;

            UUID groupID = gm.CreateGroup(tc, "group1", null, true, UUID.Zero, 0, true, true, true);
            gm.JoinGroupRequest(tc, groupID);

            // Create a second user who doesn't want to receive notices
            ScenePresence sp2 = SceneHelpers.AddScenePresence(scene, TestHelpers.ParseTail(0x2));
            TestClient tc2 = (TestClient)sp2.ControllingClient;
            gm.JoinGroupRequest(tc2, groupID);
            gm.SetGroupAcceptNotices(tc2, groupID, false, true);

            List<GridInstantMessage> spReceivedMessages = new List<GridInstantMessage>();
            tc.OnReceivedInstantMessage += im => spReceivedMessages.Add(im);

            List<GridInstantMessage> sp2ReceivedMessages = new List<GridInstantMessage>();
            tc2.OnReceivedInstantMessage += im => sp2ReceivedMessages.Add(im);

            GridInstantMessage noticeIm = new GridInstantMessage();
            noticeIm.fromAgentID = userId.Guid;
            noticeIm.toAgentID = groupID.Guid;
            noticeIm.message = combinedSubjectMessage;
            noticeIm.dialog = (byte)InstantMessageDialog.GroupNotice;

            tc.HandleImprovedInstantMessage(noticeIm);

            Assert.That(spReceivedMessages.Count, Is.EqualTo(1));
            Assert.That(spReceivedMessages[0].message, Is.EqualTo(combinedSubjectMessage));

            Assert.That(sp2ReceivedMessages.Count, Is.EqualTo(0));
        }
    }
}