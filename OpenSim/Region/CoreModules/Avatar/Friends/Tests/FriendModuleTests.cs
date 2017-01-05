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
using Nini.Config;
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Data.Null;
using OpenSim.Framework;
using OpenSim.Region.CoreModules.Avatar.Friends;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Tests.Common;

namespace OpenSim.Region.CoreModules.Avatar.Friends.Tests
{
    [TestFixture]
    public class FriendsModuleTests : OpenSimTestCase
    {
        private FriendsModule m_fm;
        private TestScene m_scene;

        [TestFixtureSetUp]
        public void FixtureInit()
        {
            // Don't allow tests to be bamboozled by asynchronous events.  Execute everything on the same thread.
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

        [SetUp]
        public void Init()
        {
            // We must clear friends data between tests since Data.Null holds it in static properties.  This is necessary
            // so that different services and simulator can share the data in standalone mode.  This is pretty horrible
            // effectively the statics are global variables.
            NullFriendsData.Clear();

            IConfigSource config = new IniConfigSource();
            config.AddConfig("Modules");
            // Not strictly necessary since FriendsModule assumes it is the default (!)
            config.Configs["Modules"].Set("FriendsModule", "FriendsModule");
            config.AddConfig("Friends");
            config.Configs["Friends"].Set("Connector", "OpenSim.Services.FriendsService.dll");
            config.AddConfig("FriendsService");
            config.Configs["FriendsService"].Set("StorageProvider", "OpenSim.Data.Null.dll");

            m_scene = new SceneHelpers().SetupScene();
            m_fm = new FriendsModule();
            SceneHelpers.SetupSceneModules(m_scene, config, m_fm);
        }

        [Test]
        public void TestLoginWithNoFriends()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            UUID userId = TestHelpers.ParseTail(0x1);

            ScenePresence sp = SceneHelpers.AddScenePresence(m_scene, userId);

            Assert.That(((TestClient)sp.ControllingClient).ReceivedOfflineNotifications.Count, Is.EqualTo(0));
            Assert.That(((TestClient)sp.ControllingClient).ReceivedOnlineNotifications.Count, Is.EqualTo(0));
        }

        [Test]
        public void TestLoginWithOfflineFriends()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            UUID user1Id = TestHelpers.ParseTail(0x1);
            UUID user2Id = TestHelpers.ParseTail(0x2);

//            UserAccountHelpers.CreateUserWithInventory(m_scene, user1Id);
//            UserAccountHelpers.CreateUserWithInventory(m_scene, user2Id);
//
//            m_fm.AddFriendship(user1Id, user2Id);

            ScenePresence sp1 = SceneHelpers.AddScenePresence(m_scene, user1Id);
            ScenePresence sp2 = SceneHelpers.AddScenePresence(m_scene, user2Id);

            m_fm.AddFriendship(sp1.ControllingClient, user2Id);

            // Not necessary for this test.  CanSeeOnline is automatically granted.
//            m_fm.GrantRights(sp1.ControllingClient, user2Id, (int)FriendRights.CanSeeOnline);

            // We must logout from the client end so that the presence service is correctly updated by the presence
            // detector.  This is listening to the OnConnectionClosed event on the client.
            ((TestClient)sp1.ControllingClient).Logout();
            ((TestClient)sp2.ControllingClient).Logout();
//            m_scene.RemoveClient(sp1.UUID, true);
//            m_scene.RemoveClient(sp2.UUID, true);

            ScenePresence sp1Redux = SceneHelpers.AddScenePresence(m_scene, user1Id);

            // We don't expect to receive notifications of offline friends on login, just online.
            Assert.That(((TestClient)sp1Redux.ControllingClient).ReceivedOfflineNotifications.Count, Is.EqualTo(0));
            Assert.That(((TestClient)sp1Redux.ControllingClient).ReceivedOnlineNotifications.Count, Is.EqualTo(0));
        }

        [Test]
        public void TestLoginWithOnlineFriends()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            UUID user1Id = TestHelpers.ParseTail(0x1);
            UUID user2Id = TestHelpers.ParseTail(0x2);

//            UserAccountHelpers.CreateUserWithInventory(m_scene, user1Id);
//            UserAccountHelpers.CreateUserWithInventory(m_scene, user2Id);
//
//            m_fm.AddFriendship(user1Id, user2Id);

            ScenePresence sp1 = SceneHelpers.AddScenePresence(m_scene, user1Id);
            ScenePresence sp2 = SceneHelpers.AddScenePresence(m_scene, user2Id);

            m_fm.AddFriendship(sp1.ControllingClient, user2Id);

            // Not necessary for this test.  CanSeeOnline is automatically granted.
//            m_fm.GrantRights(sp1.ControllingClient, user2Id, (int)FriendRights.CanSeeOnline);

            // We must logout from the client end so that the presence service is correctly updated by the presence
            // detector.  This is listening to the OnConnectionClosed event on the client.
//            ((TestClient)sp1.ControllingClient).Logout();
            ((TestClient)sp2.ControllingClient).Logout();
//            m_scene.RemoveClient(user2Id, true);

            ScenePresence sp2Redux = SceneHelpers.AddScenePresence(m_scene, user2Id);

            Assert.That(((TestClient)sp2Redux.ControllingClient).ReceivedOfflineNotifications.Count, Is.EqualTo(0));
            Assert.That(((TestClient)sp2Redux.ControllingClient).ReceivedOnlineNotifications.Count, Is.EqualTo(1));
        }

        [Test]
        public void TestAddFriendshipWhileOnline()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            UUID userId = TestHelpers.ParseTail(0x1);
            UUID user2Id = TestHelpers.ParseTail(0x2);

            ScenePresence sp = SceneHelpers.AddScenePresence(m_scene, userId);
            SceneHelpers.AddScenePresence(m_scene, user2Id);

            // This fiendship is two-way but without a connector, only the first user will receive the online
            // notification.
            m_fm.AddFriendship(sp.ControllingClient, user2Id);

            Assert.That(((TestClient)sp.ControllingClient).ReceivedOfflineNotifications.Count, Is.EqualTo(0));
            Assert.That(((TestClient)sp.ControllingClient).ReceivedOnlineNotifications.Count, Is.EqualTo(1));
        }

        [Test]
        public void TestRemoveFriendshipWhileOnline()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            UUID user1Id = TestHelpers.ParseTail(0x1);
            UUID user2Id = TestHelpers.ParseTail(0x2);

            ScenePresence sp = SceneHelpers.AddScenePresence(m_scene, user1Id);
            SceneHelpers.AddScenePresence(m_scene, user2Id);

            m_fm.AddFriendship(sp.ControllingClient, user2Id);
            m_fm.RemoveFriendship(sp.ControllingClient, user2Id);

            TestClient user1Client = sp.ControllingClient as TestClient;
            Assert.That(user1Client.ReceivedFriendshipTerminations.Count, Is.EqualTo(1));
            Assert.That(user1Client.ReceivedFriendshipTerminations[0], Is.EqualTo(user2Id));
        }
    }
}