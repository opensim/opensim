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
using System.Reflection;
using System.Text;
using System.Threading;
using System.Timers;
using Timer=System.Timers.Timer;
using Nini.Config;
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Region.CoreModules.Avatar.Attachments;
using OpenSim.Region.CoreModules.Framework.InventoryAccess;
using OpenSim.Region.CoreModules.World.Serialiser;
using OpenSim.Region.CoreModules.ServiceConnectorsOut.Simulation;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Tests.Common;
using OpenSim.Tests.Common.Mock;

namespace OpenSim.Region.CoreModules.Avatar.Attachments.Tests
{
    /// <summary>
    /// Attachment tests
    /// </summary>
    [TestFixture]
    public class AttachmentsModuleTests
    {
        private Scene scene;
        private AttachmentsModule m_attMod;

        [SetUp]
        public void Init()
        {
            // Don't allow tests to be bamboozled by asynchronous events.  Execute everything on the same thread.
            Util.FireAndForgetMethod = FireAndForgetMethod.None;

            IConfigSource config = new IniConfigSource();
            config.AddConfig("Modules");
            config.Configs["Modules"].Set("InventoryAccessModule", "BasicInventoryAccessModule");

            scene = SceneHelpers.SetupScene();
            m_attMod = new AttachmentsModule();
            SceneHelpers.SetupSceneModules(scene, config, m_attMod, new BasicInventoryAccessModule());
        }

        [TearDown]
        public void TearDown()
        {
            // We must set this back afterwards, otherwise later tests will fail since they're expecting multiple
            // threads.  Possibly, later tests should be rewritten not to worry about such things.
            Util.FireAndForgetMethod = Util.DefaultFireAndForgetMethod;
        }

        [Test]
        public void TestAddAttachmentFromGround()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            UUID userId = TestHelpers.ParseTail(0x1);
            string attName = "att";

            UserAccountHelpers.CreateUserWithInventory(scene, userId);
            ScenePresence presence = SceneHelpers.AddScenePresence(scene, userId);
            SceneObjectGroup so = SceneHelpers.AddSceneObject(scene, attName).ParentGroup;

            m_attMod.AttachObject(presence.ControllingClient, so, (uint)AttachmentPoint.Chest, false);

            // Check status on scene presence
            Assert.That(presence.HasAttachments(), Is.True);
            List<SceneObjectGroup> attachments = presence.Attachments;
            Assert.That(attachments.Count, Is.EqualTo(1));
            SceneObjectGroup attSo = attachments[0];
            Assert.That(attSo.Name, Is.EqualTo(attName));
            Assert.That(attSo.GetAttachmentPoint(), Is.EqualTo((byte)AttachmentPoint.Chest));
            Assert.That(attSo.IsAttachment);
            Assert.That(attSo.UsesPhysics, Is.False);
            Assert.That(attSo.IsTemporary, Is.False);

            // Check item status
            Assert.That(presence.Appearance.GetAttachpoint(
                attSo.GetFromItemID()), Is.EqualTo((int)AttachmentPoint.Chest));
        }

        [Test]
        public void TestAddAttachmentFromInventory()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            UUID userId = TestHelpers.ParseTail(0x1);
            UUID attItemId = TestHelpers.ParseTail(0x2);
            UUID attAssetId = TestHelpers.ParseTail(0x3);
            string attName = "att";

            UserAccountHelpers.CreateUserWithInventory(scene, userId);
            ScenePresence presence = SceneHelpers.AddScenePresence(scene, userId);
            InventoryItemBase attItem
                = UserInventoryHelpers.CreateInventoryItem(
                    scene, attName, attItemId, attAssetId, userId, InventoryType.Object);

            m_attMod.RezSingleAttachmentFromInventory(
                presence.ControllingClient, attItemId, (uint)AttachmentPoint.Chest);

            // Check status on scene presence
            Assert.That(presence.HasAttachments(), Is.True);
            List<SceneObjectGroup> attachments = presence.Attachments;
            Assert.That(attachments.Count, Is.EqualTo(1));
            SceneObjectGroup attSo = attachments[0];
            Assert.That(attSo.Name, Is.EqualTo(attName));
            Assert.That(attSo.GetAttachmentPoint(), Is.EqualTo((byte)AttachmentPoint.Chest));
            Assert.That(attSo.IsAttachment);
            Assert.That(attSo.UsesPhysics, Is.False);
            Assert.That(attSo.IsTemporary, Is.False);

            // Check item status
            Assert.That(presence.Appearance.GetAttachpoint(attItemId), Is.EqualTo((int)AttachmentPoint.Chest));
        }

        [Test]
        public void TestDetachAttachmentToScene()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            UUID userId = TestHelpers.ParseTail(0x1);
            UUID attItemId = TestHelpers.ParseTail(0x2);
            UUID attAssetId = TestHelpers.ParseTail(0x3);
            string attName = "att";

            UserAccountHelpers.CreateUserWithInventory(scene, userId);
            ScenePresence presence = SceneHelpers.AddScenePresence(scene, userId);
            InventoryItemBase attItem
                = UserInventoryHelpers.CreateInventoryItem(
                    scene, attName, attItemId, attAssetId, userId, InventoryType.Object);

            UUID attSoId = m_attMod.RezSingleAttachmentFromInventory(
                presence.ControllingClient, attItemId, (uint)AttachmentPoint.Chest);
            m_attMod.DetachSingleAttachmentToGround(attSoId, presence.ControllingClient);

            // Check status on scene presence
            Assert.That(presence.HasAttachments(), Is.False);
            List<SceneObjectGroup> attachments = presence.Attachments;
            Assert.That(attachments.Count, Is.EqualTo(0));

            // Check item status
            Assert.That(scene.InventoryService.GetItem(new InventoryItemBase(attItemId)), Is.Null);

            // Check object in scene
            Assert.That(scene.GetSceneObjectGroup("att"), Is.Not.Null);
        }

        [Test]
        public void TestDetachAttachmentToInventory()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            UUID userId = TestHelpers.ParseTail(0x1);
            UUID attItemId = TestHelpers.ParseTail(0x2);
            UUID attAssetId = TestHelpers.ParseTail(0x3);
            string attName = "att";

            UserAccountHelpers.CreateUserWithInventory(scene, userId);
            ScenePresence presence = SceneHelpers.AddScenePresence(scene, userId);
            InventoryItemBase attItem
                = UserInventoryHelpers.CreateInventoryItem(
                    scene, attName, attItemId, attAssetId, userId, InventoryType.Object);

            m_attMod.RezSingleAttachmentFromInventory(
                presence.ControllingClient, attItemId, (uint)AttachmentPoint.Chest);
            m_attMod.DetachSingleAttachmentToInv(attItemId, presence.ControllingClient);

            // Check status on scene presence
            Assert.That(presence.HasAttachments(), Is.False);
            List<SceneObjectGroup> attachments = presence.Attachments;
            Assert.That(attachments.Count, Is.EqualTo(0));

            // Check item status
            Assert.That(presence.Appearance.GetAttachpoint(attItemId), Is.EqualTo(0));
        }

        [Test]
        public void TestRezAttachmentsOnAvatarEntrance()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            UUID userId = TestHelpers.ParseTail(0x1);
            UUID attItemId = TestHelpers.ParseTail(0x2);
            UUID attAssetId = TestHelpers.ParseTail(0x3);
            string attName = "att";

            UserAccountHelpers.CreateUserWithInventory(scene, userId);
            InventoryItemBase attItem
                = UserInventoryHelpers.CreateInventoryItem(
                    scene, attName, attItemId, attAssetId, userId, InventoryType.Object);

            AgentCircuitData acd = SceneHelpers.GenerateAgentData(userId);
            acd.Appearance = new AvatarAppearance();
            acd.Appearance.SetAttachment((int)AttachmentPoint.Chest, attItem.ID, attItem.AssetID);
            ScenePresence presence = SceneHelpers.AddScenePresence(scene, acd);

            Assert.That(presence.HasAttachments(), Is.True);
            List<SceneObjectGroup> attachments = presence.Attachments;

            Assert.That(attachments.Count, Is.EqualTo(1));
            SceneObjectGroup attSo = attachments[0];
            Assert.That(attSo.Name, Is.EqualTo(attName));
            Assert.That(attSo.GetAttachmentPoint(), Is.EqualTo((byte)AttachmentPoint.Chest));
            Assert.That(attSo.IsAttachment);
            Assert.That(attSo.UsesPhysics, Is.False);
            Assert.That(attSo.IsTemporary, Is.False);
        }

        // I'm commenting this test because scene setup NEEDS InventoryService to 
        // be non-null
        //[Test]
//        public void T032_CrossAttachments()
//        {
//            TestHelpers.InMethod();
//
//            ScenePresence presence = scene.GetScenePresence(agent1);
//            ScenePresence presence2 = scene2.GetScenePresence(agent1);
//            presence2.AddAttachment(sog1);
//            presence2.AddAttachment(sog2);
//
//            ISharedRegionModule serialiser = new SerialiserModule();
//            SceneHelpers.SetupSceneModules(scene, new IniConfigSource(), serialiser);
//            SceneHelpers.SetupSceneModules(scene2, new IniConfigSource(), serialiser);
//
//            Assert.That(presence.HasAttachments(), Is.False, "Presence has attachments before cross");
//
//            //Assert.That(presence2.CrossAttachmentsIntoNewRegion(region1, true), Is.True, "Cross was not successful");
//            Assert.That(presence2.HasAttachments(), Is.False, "Presence2 objects were not deleted");
//            Assert.That(presence.HasAttachments(), Is.True, "Presence has not received new objects");
//        }
    }
}