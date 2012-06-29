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
using OpenSim.Region.CoreModules.Framework;
using OpenSim.Region.CoreModules.Framework.EntityTransfer;
using OpenSim.Region.CoreModules.Framework.InventoryAccess;
using OpenSim.Region.CoreModules.World.Serialiser;
using OpenSim.Region.CoreModules.ServiceConnectorsOut.Simulation;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Services.Interfaces;
using OpenSim.Tests.Common;
using OpenSim.Tests.Common.Mock;

namespace OpenSim.Region.CoreModules.Avatar.Attachments.Tests
{
    /// <summary>
    /// Attachment tests
    /// </summary>
    [TestFixture]
    public class AttachmentsModuleTests : OpenSimTestCase
    {
        [TestFixtureSetUp]
        public void FixtureInit()
        {
            // Don't allow tests to be bamboozled by asynchronous events.  Execute everything on the same thread.
            Util.FireAndForgetMethod = FireAndForgetMethod.None;
        }

        [TestFixtureTearDown]
        public void TearDown()
        {
            // We must set this back afterwards, otherwise later tests will fail since they're expecting multiple
            // threads.  Possibly, later tests should be rewritten not to worry about such things.
            Util.FireAndForgetMethod = Util.DefaultFireAndForgetMethod;
        }

        private Scene CreateDefaultTestScene()
        {
            IConfigSource config = new IniConfigSource();
            config.AddConfig("Modules");
            config.Configs["Modules"].Set("InventoryAccessModule", "BasicInventoryAccessModule");

            Scene scene = new SceneHelpers().SetupScene();
            SceneHelpers.SetupSceneModules(scene, config, new AttachmentsModule(), new BasicInventoryAccessModule());

            return scene;
        }

        /// <summary>
        /// Creates an attachment item in the given user's inventory.  Does not attach.
        /// </summary>
        /// <remarks>
        /// A user with the given ID and an inventory must already exist.
        /// </remarks>
        /// <returns>
        /// The attachment item.
        /// </returns>
        /// <param name='scene'></param>
        /// <param name='userId'></param>
        /// <param name='attName'></param>
        /// <param name='rawItemId'></param>
        /// <param name='rawAssetId'></param>
        private InventoryItemBase CreateAttachmentItem(
            Scene scene, UUID userId, string attName, int rawItemId, int rawAssetId)
        {
            return UserInventoryHelpers.CreateInventoryItem(
                scene,
                attName,
                TestHelpers.ParseTail(rawItemId),
                TestHelpers.ParseTail(rawAssetId),
                userId,
                InventoryType.Object);
        }

        [Test]
        public void TestAddAttachmentFromGround()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            Scene scene = CreateDefaultTestScene();
            UserAccount ua1 = UserAccountHelpers.CreateUserWithInventory(scene, 0x1);
            ScenePresence sp = SceneHelpers.AddScenePresence(scene, ua1.PrincipalID);

            string attName = "att";

            SceneObjectGroup so = SceneHelpers.AddSceneObject(scene, attName, sp.UUID).ParentGroup;

            scene.AttachmentsModule.AttachObject(sp, so, (uint)AttachmentPoint.Chest, false, false);

            // Check status on scene presence
            Assert.That(sp.HasAttachments(), Is.True);
            List<SceneObjectGroup> attachments = sp.GetAttachments();
            Assert.That(attachments.Count, Is.EqualTo(1));
            SceneObjectGroup attSo = attachments[0];
            Assert.That(attSo.Name, Is.EqualTo(attName));
            Assert.That(attSo.AttachmentPoint, Is.EqualTo((byte)AttachmentPoint.Chest));
            Assert.That(attSo.IsAttachment);
            Assert.That(attSo.UsesPhysics, Is.False);
            Assert.That(attSo.IsTemporary, Is.False);

            // Check item status
            Assert.That(
                sp.Appearance.GetAttachpoint(attSo.FromItemID),
                Is.EqualTo((int)AttachmentPoint.Chest));

            InventoryItemBase attachmentItem = scene.InventoryService.GetItem(new InventoryItemBase(attSo.FromItemID));
            Assert.That(attachmentItem, Is.Not.Null);
            Assert.That(attachmentItem.Name, Is.EqualTo(attName));

            InventoryFolderBase targetFolder = scene.InventoryService.GetFolderForType(sp.UUID, AssetType.Object);
            Assert.That(attachmentItem.Folder, Is.EqualTo(targetFolder.ID));

//            TestHelpers.DisableLogging();
        }

        [Test]
        public void TestAddAttachmentFromInventory()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            Scene scene = CreateDefaultTestScene();
            UserAccount ua1 = UserAccountHelpers.CreateUserWithInventory(scene, 0x1);
            ScenePresence sp = SceneHelpers.AddScenePresence(scene, ua1.PrincipalID);

            InventoryItemBase attItem = CreateAttachmentItem(scene, ua1.PrincipalID, "att", 0x10, 0x20);

            scene.AttachmentsModule.RezSingleAttachmentFromInventory(
                sp, attItem.ID, (uint)AttachmentPoint.Chest);

            // Check scene presence status
            Assert.That(sp.HasAttachments(), Is.True);
            List<SceneObjectGroup> attachments = sp.GetAttachments();
            Assert.That(attachments.Count, Is.EqualTo(1));
            SceneObjectGroup attSo = attachments[0];
            Assert.That(attSo.Name, Is.EqualTo(attItem.Name));
            Assert.That(attSo.AttachmentPoint, Is.EqualTo((byte)AttachmentPoint.Chest));
            Assert.That(attSo.IsAttachment);
            Assert.That(attSo.UsesPhysics, Is.False);
            Assert.That(attSo.IsTemporary, Is.False);

            // Check appearance status
            Assert.That(sp.Appearance.GetAttachments().Count, Is.EqualTo(1));
            Assert.That(sp.Appearance.GetAttachpoint(attItem.ID), Is.EqualTo((int)AttachmentPoint.Chest));
        }

        [Test]
        public void TestDetachAttachmentToGround()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            Scene scene = CreateDefaultTestScene();
            UserAccount ua1 = UserAccountHelpers.CreateUserWithInventory(scene, 0x1);
            ScenePresence sp = SceneHelpers.AddScenePresence(scene, ua1.PrincipalID);

            InventoryItemBase attItem = CreateAttachmentItem(scene, ua1.PrincipalID, "att", 0x10, 0x20);

            ISceneEntity so
                = scene.AttachmentsModule.RezSingleAttachmentFromInventory(
                    sp, attItem.ID, (uint)AttachmentPoint.Chest);
            scene.AttachmentsModule.DetachSingleAttachmentToGround(sp, so.LocalId);

            // Check scene presence status
            Assert.That(sp.HasAttachments(), Is.False);
            List<SceneObjectGroup> attachments = sp.GetAttachments();
            Assert.That(attachments.Count, Is.EqualTo(0));

            // Check appearance status
            Assert.That(sp.Appearance.GetAttachments().Count, Is.EqualTo(0));

            // Check item status
            Assert.That(scene.InventoryService.GetItem(new InventoryItemBase(attItem.ID)), Is.Null);

            // Check object in scene
            Assert.That(scene.GetSceneObjectGroup("att"), Is.Not.Null);
        }

        [Test]
        public void TestDetachAttachmentToInventory()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            Scene scene = CreateDefaultTestScene();
            UserAccount ua1 = UserAccountHelpers.CreateUserWithInventory(scene, 0x1);
            ScenePresence sp = SceneHelpers.AddScenePresence(scene, ua1.PrincipalID);

            InventoryItemBase attItem = CreateAttachmentItem(scene, ua1.PrincipalID, "att", 0x10, 0x20);

            SceneObjectGroup so
                = (SceneObjectGroup)scene.AttachmentsModule.RezSingleAttachmentFromInventory(
                    sp, attItem.ID, (uint)AttachmentPoint.Chest);
            scene.AttachmentsModule.DetachSingleAttachmentToInv(sp, so);

            // Check status on scene presence
            Assert.That(sp.HasAttachments(), Is.False);
            List<SceneObjectGroup> attachments = sp.GetAttachments();
            Assert.That(attachments.Count, Is.EqualTo(0));

            // Check item status
            Assert.That(sp.Appearance.GetAttachpoint(attItem.ID), Is.EqualTo(0));
        }

        /// <summary>
        /// Test that attachments don't hang about in the scene when the agent is closed
        /// </summary>
        [Test]
        public void TestRemoveAttachmentsOnAvatarExit()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            Scene scene = CreateDefaultTestScene();
            UserAccount ua1 = UserAccountHelpers.CreateUserWithInventory(scene, 0x1);
            InventoryItemBase attItem = CreateAttachmentItem(scene, ua1.PrincipalID, "att", 0x10, 0x20);

            AgentCircuitData acd = SceneHelpers.GenerateAgentData(ua1.PrincipalID);
            acd.Appearance = new AvatarAppearance();
            acd.Appearance.SetAttachment((int)AttachmentPoint.Chest, attItem.ID, attItem.AssetID);
            ScenePresence presence = SceneHelpers.AddScenePresence(scene, acd);

            SceneObjectGroup rezzedAtt = presence.GetAttachments()[0];

            scene.IncomingCloseAgent(presence.UUID);

            // Check that we can't retrieve this attachment from the scene.
            Assert.That(scene.GetSceneObjectGroup(rezzedAtt.UUID), Is.Null);
        }

        [Test]
        public void TestRezAttachmentsOnAvatarEntrance()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            Scene scene = CreateDefaultTestScene();
            UserAccount ua1 = UserAccountHelpers.CreateUserWithInventory(scene, 0x1);
            InventoryItemBase attItem = CreateAttachmentItem(scene, ua1.PrincipalID, "att", 0x10, 0x20);

            AgentCircuitData acd = SceneHelpers.GenerateAgentData(ua1.PrincipalID);
            acd.Appearance = new AvatarAppearance();
            acd.Appearance.SetAttachment((int)AttachmentPoint.Chest, attItem.ID, attItem.AssetID);
            ScenePresence presence = SceneHelpers.AddScenePresence(scene, acd);

            Assert.That(presence.HasAttachments(), Is.True);
            List<SceneObjectGroup> attachments = presence.GetAttachments();

            Assert.That(attachments.Count, Is.EqualTo(1));
            SceneObjectGroup attSo = attachments[0];
            Assert.That(attSo.Name, Is.EqualTo(attItem.Name));
            Assert.That(attSo.AttachmentPoint, Is.EqualTo((byte)AttachmentPoint.Chest));
            Assert.That(attSo.IsAttachment);
            Assert.That(attSo.UsesPhysics, Is.False);
            Assert.That(attSo.IsTemporary, Is.False);

            // Check appearance status
            List<AvatarAttachment> retreivedAttachments = presence.Appearance.GetAttachments();
            Assert.That(retreivedAttachments.Count, Is.EqualTo(1));
            Assert.That(retreivedAttachments[0].AttachPoint, Is.EqualTo((int)AttachmentPoint.Chest));
            Assert.That(retreivedAttachments[0].ItemID, Is.EqualTo(attItem.ID));
            Assert.That(retreivedAttachments[0].AssetID, Is.EqualTo(attItem.AssetID));
            Assert.That(presence.Appearance.GetAttachpoint(attItem.ID), Is.EqualTo((int)AttachmentPoint.Chest));
        }

        [Test]
        public void TestUpdateAttachmentPosition()
        {
            TestHelpers.InMethod();

            Scene scene = CreateDefaultTestScene();
            UserAccount ua1 = UserAccountHelpers.CreateUserWithInventory(scene, 0x1);
            InventoryItemBase attItem = CreateAttachmentItem(scene, ua1.PrincipalID, "att", 0x10, 0x20);

            AgentCircuitData acd = SceneHelpers.GenerateAgentData(ua1.PrincipalID);
            acd.Appearance = new AvatarAppearance();
            acd.Appearance.SetAttachment((int)AttachmentPoint.Chest, attItem.ID, attItem.AssetID);
            ScenePresence sp = SceneHelpers.AddScenePresence(scene, acd);

            SceneObjectGroup attSo = sp.GetAttachments()[0];

            Vector3 newPosition = new Vector3(1, 2, 4);

            scene.SceneGraph.UpdatePrimGroupPosition(attSo.LocalId, newPosition, sp.ControllingClient);

            Assert.That(attSo.AbsolutePosition, Is.EqualTo(sp.AbsolutePosition));
            Assert.That(attSo.RootPart.AttachedPos, Is.EqualTo(newPosition));
        }

        [Test]
        public void TestSameSimulatorNeighbouringRegionsTeleport()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            AttachmentsModule attModA = new AttachmentsModule();
            AttachmentsModule attModB = new AttachmentsModule();
            EntityTransferModule etmA = new EntityTransferModule();
            EntityTransferModule etmB = new EntityTransferModule();
            LocalSimulationConnectorModule lscm = new LocalSimulationConnectorModule();

            IConfigSource config = new IniConfigSource();
            IConfig modulesConfig = config.AddConfig("Modules");
            modulesConfig.Set("EntityTransferModule", etmA.Name);
            modulesConfig.Set("SimulationServices", lscm.Name);
            IConfig entityTransferConfig = config.AddConfig("EntityTransfer");

            // In order to run a single threaded regression test we do not want the entity transfer module waiting
            // for a callback from the destination scene before removing its avatar data.
            entityTransferConfig.Set("wait_for_callback", false);

            modulesConfig.Set("InventoryAccessModule", "BasicInventoryAccessModule");

            SceneHelpers sh = new SceneHelpers();
            TestScene sceneA = sh.SetupScene("sceneA", TestHelpers.ParseTail(0x100), 1000, 1000);
            TestScene sceneB = sh.SetupScene("sceneB", TestHelpers.ParseTail(0x200), 1001, 1000);

            SceneHelpers.SetupSceneModules(new Scene[] { sceneA, sceneB }, config, lscm);
            SceneHelpers.SetupSceneModules(
                sceneA, config, new CapabilitiesModule(), etmA, attModA, new BasicInventoryAccessModule());
            SceneHelpers.SetupSceneModules(
                sceneB, config, new CapabilitiesModule(), etmB, attModB, new BasicInventoryAccessModule());

            UserAccount ua1 = UserAccountHelpers.CreateUserWithInventory(sceneA, 0x1);
            ScenePresence beforeTeleportSp = SceneHelpers.AddScenePresence(sceneA, ua1.PrincipalID, sh.SceneManager);
            beforeTeleportSp.AbsolutePosition = new Vector3(30, 31, 32);

            InventoryItemBase attItem = CreateAttachmentItem(sceneA, ua1.PrincipalID, "att", 0x10, 0x20);

            sceneA.AttachmentsModule.RezSingleAttachmentFromInventory(
                beforeTeleportSp, attItem.ID, (uint)AttachmentPoint.Chest);

            Vector3 teleportPosition = new Vector3(10, 11, 12);
            Vector3 teleportLookAt = new Vector3(20, 21, 22);

            sceneA.RequestTeleportLocation(
                beforeTeleportSp.ControllingClient,
                sceneB.RegionInfo.RegionHandle,
                teleportPosition,
                teleportLookAt,
                (uint)TeleportFlags.ViaLocation);

            ((TestClient)beforeTeleportSp.ControllingClient).CompleteTeleportClientSide();

            // Check attachments have made it into sceneB
            ScenePresence afterTeleportSceneBSp = sceneB.GetScenePresence(ua1.PrincipalID);

            // This is appearance data, as opposed to actually rezzed attachments
            List<AvatarAttachment> sceneBAttachments = afterTeleportSceneBSp.Appearance.GetAttachments();
            Assert.That(sceneBAttachments.Count, Is.EqualTo(1));
            Assert.That(sceneBAttachments[0].AttachPoint, Is.EqualTo((int)AttachmentPoint.Chest));
            Assert.That(sceneBAttachments[0].ItemID, Is.EqualTo(attItem.ID));
            Assert.That(sceneBAttachments[0].AssetID, Is.EqualTo(attItem.AssetID));
            Assert.That(afterTeleportSceneBSp.Appearance.GetAttachpoint(attItem.ID), Is.EqualTo((int)AttachmentPoint.Chest));

            // This is the actual attachment
            List<SceneObjectGroup> actualSceneBAttachments = afterTeleportSceneBSp.GetAttachments();
            Assert.That(actualSceneBAttachments.Count, Is.EqualTo(1));
            SceneObjectGroup actualSceneBAtt = actualSceneBAttachments[0];
            Assert.That(actualSceneBAtt.Name, Is.EqualTo(attItem.Name));
            Assert.That(actualSceneBAtt.AttachmentPoint, Is.EqualTo((uint)AttachmentPoint.Chest));

            // Check attachments have been removed from sceneA
            ScenePresence afterTeleportSceneASp = sceneA.GetScenePresence(ua1.PrincipalID);

            // Since this is appearance data, it is still present on the child avatar!
            List<AvatarAttachment> sceneAAttachments = afterTeleportSceneASp.Appearance.GetAttachments();
            Assert.That(sceneAAttachments.Count, Is.EqualTo(1));
            Assert.That(afterTeleportSceneASp.Appearance.GetAttachpoint(attItem.ID), Is.EqualTo((int)AttachmentPoint.Chest));

            // This is the actual attachment, which should no longer exist
            List<SceneObjectGroup> actualSceneAAttachments = afterTeleportSceneASp.GetAttachments();
            Assert.That(actualSceneAAttachments.Count, Is.EqualTo(0));
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
