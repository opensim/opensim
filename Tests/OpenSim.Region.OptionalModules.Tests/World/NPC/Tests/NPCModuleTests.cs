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
using log4net;
using Nini.Config;
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.CoreModules.Avatar.Attachments;
using OpenSim.Region.CoreModules.Avatar.AvatarFactory;
using OpenSim.Region.CoreModules.Framework.InventoryAccess;
using OpenSim.Region.CoreModules.Framework.UserManagement;
using OpenSim.Region.CoreModules.ServiceConnectorsOut.Avatar;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.AvatarService;
using OpenSim.Tests.Common;

namespace OpenSim.Region.OptionalModules.World.NPC.Tests
{
    [TestFixture]
    public class NPCModuleTests : OpenSimTestCase
    {
        private TestScene m_scene;
        private AvatarFactoryModule m_afMod;
        private UserManagementModule m_umMod;
        private AttachmentsModule m_attMod;
        private NPCModule m_npcMod;

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

        public void SetUpScene()
        {
            SetUpScene(256, 256);
        }

        public void SetUpScene(uint sizeX, uint sizeY)
        {
            IConfigSource config = new IniConfigSource();
            config.AddConfig("NPC");
            config.Configs["NPC"].Set("Enabled", "true");
            config.AddConfig("Modules");
            config.Configs["Modules"].Set("InventoryAccessModule", "BasicInventoryAccessModule");

            m_afMod = new AvatarFactoryModule();
            m_umMod = new UserManagementModule();
            m_attMod = new AttachmentsModule();
            m_npcMod = new NPCModule();

            m_scene = new SceneHelpers().SetupScene("test scene", UUID.Random(), 1000, 1000, sizeX, sizeY, config);
            SceneHelpers.SetupSceneModules(m_scene, config, m_afMod, m_umMod, m_attMod, m_npcMod, new BasicInventoryAccessModule());
        }

        [Test]
        public void TestCreate()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            SetUpScene();

            ScenePresence sp = SceneHelpers.AddScenePresence(m_scene, TestHelpers.ParseTail(0x1));
//            ScenePresence originalAvatar = scene.GetScenePresence(originalClient.AgentId);

            // 8 is the index of the first baked texture in AvatarAppearance
            UUID originalFace8TextureId = TestHelpers.ParseTail(0x10);
            Primitive.TextureEntry originalTe = new Primitive.TextureEntry(UUID.Zero);
            Primitive.TextureEntryFace originalTef = originalTe.CreateFace(8);
            originalTef.TextureID = originalFace8TextureId;

            // We also need to add the texture to the asset service, otherwise the AvatarFactoryModule will tell
            // ScenePresence.SendInitialData() to reset our entire appearance.
            m_scene.AssetService.Store(AssetHelpers.CreateNotecardAsset(originalFace8TextureId));

            m_afMod.SetAppearance(sp, originalTe, null, new WearableCacheItem[0] );

            UUID npcId = m_npcMod.CreateNPC("John", "Smith", new Vector3(128, 128, 30), UUID.Zero, true, m_scene, sp.Appearance);

            ScenePresence npc = m_scene.GetScenePresence(npcId);

            Assert.That(npc, Is.Not.Null);
            Assert.That(npc.Appearance.Texture.FaceTextures[8].TextureID, Is.EqualTo(originalFace8TextureId));
            Assert.That(m_umMod.GetUserName(npc.UUID), Is.EqualTo(string.Format("{0} {1}", npc.Firstname, npc.Lastname)));

            IClientAPI client;
            Assert.That(m_scene.TryGetClient(npcId, out client), Is.True);

            // Have to account for both SP and NPC.
            Assert.That(m_scene.AuthenticateHandler.GetAgentCircuits().Count, Is.EqualTo(2));
        }

        [Test]
        public void TestRemove()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            SetUpScene();

            ScenePresence sp = SceneHelpers.AddScenePresence(m_scene, TestHelpers.ParseTail(0x1));
//            ScenePresence originalAvatar = scene.GetScenePresence(originalClient.AgentId);

            Vector3 startPos = new Vector3(128, 128, 30);
            UUID npcId = m_npcMod.CreateNPC("John", "Smith", startPos, UUID.Zero, true, m_scene, sp.Appearance);

            m_npcMod.DeleteNPC(npcId, m_scene);

            ScenePresence deletedNpc = m_scene.GetScenePresence(npcId);

            Assert.That(deletedNpc, Is.Null);
            IClientAPI client;
            Assert.That(m_scene.TryGetClient(npcId, out client), Is.False);

            // Have to account for SP still present.
            Assert.That(m_scene.AuthenticateHandler.GetAgentCircuits().Count, Is.EqualTo(1));
        }

        [Test]
        public void TestCreateWithAttachments()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            SetUpScene();

            UUID userId = TestHelpers.ParseTail(0x1);
            UserAccountHelpers.CreateUserWithInventory(m_scene, userId);
            ScenePresence sp = SceneHelpers.AddScenePresence(m_scene, userId);

            UUID attItemId = TestHelpers.ParseTail(0x2);
            UUID attAssetId = TestHelpers.ParseTail(0x3);
            string attName = "att";

            UserInventoryHelpers.CreateInventoryItem(m_scene, attName, attItemId, attAssetId, sp.UUID, InventoryType.Object);

            m_attMod.RezSingleAttachmentFromInventory(sp, attItemId, (uint)AttachmentPoint.Chest);

            UUID npcId = m_npcMod.CreateNPC("John", "Smith", new Vector3(128, 128, 30), UUID.Zero, true, m_scene, sp.Appearance);

            ScenePresence npc = m_scene.GetScenePresence(npcId);

            // Check scene presence status
            Assert.That(npc.HasAttachments(), Is.True);
            List<SceneObjectGroup> attachments = npc.GetAttachments();
            Assert.That(attachments.Count, Is.EqualTo(1));
            SceneObjectGroup attSo = attachments[0];

            // Just for now, we won't test the name since this is (wrongly) the asset part name rather than the item
            // name.  TODO: Do need to fix ultimately since the item may be renamed before being passed on to an NPC.
//            Assert.That(attSo.Name, Is.EqualTo(attName));

            Assert.That(attSo.AttachmentPoint, Is.EqualTo((byte)AttachmentPoint.Chest));
            Assert.That(attSo.IsAttachment);
            Assert.That(attSo.UsesPhysics, Is.False);
            Assert.That(attSo.IsTemporary, Is.False);
            Assert.That(attSo.OwnerID, Is.EqualTo(npc.UUID));
        }

        [Test]
        public void TestCreateWithMultiAttachments()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            SetUpScene();
//            m_attMod.DebugLevel = 1;

            UUID userId = TestHelpers.ParseTail(0x1);
            UserAccountHelpers.CreateUserWithInventory(m_scene, userId);
            ScenePresence sp = SceneHelpers.AddScenePresence(m_scene, userId);

            InventoryItemBase att1Item
                = UserInventoryHelpers.CreateInventoryItem(
                    m_scene, "att1", TestHelpers.ParseTail(0x2), TestHelpers.ParseTail(0x3), sp.UUID, InventoryType.Object);
            InventoryItemBase att2Item
                = UserInventoryHelpers.CreateInventoryItem(
                    m_scene, "att2", TestHelpers.ParseTail(0x12), TestHelpers.ParseTail(0x13), sp.UUID, InventoryType.Object);

            m_attMod.RezSingleAttachmentFromInventory(sp, att1Item.ID, (uint)AttachmentPoint.Chest);
            m_attMod.RezSingleAttachmentFromInventory(sp, att2Item.ID, (uint)AttachmentPoint.Chest | 0x80);

            UUID npcId = m_npcMod.CreateNPC("John", "Smith", new Vector3(128, 128, 30), UUID.Zero, true, m_scene, sp.Appearance);

            ScenePresence npc = m_scene.GetScenePresence(npcId);

            // Check scene presence status
            Assert.That(npc.HasAttachments(), Is.True);
            List<SceneObjectGroup> attachments = npc.GetAttachments();
            Assert.That(attachments.Count, Is.EqualTo(2));

            // Just for now, we won't test the name since this is (wrongly) the asset part name rather than the item
            // name.  TODO: Do need to fix ultimately since the item may be renamed before being passed on to an NPC.
//            Assert.That(attSo.Name, Is.EqualTo(attName));

            TestAttachedObject(attachments[0], AttachmentPoint.Chest, npc.UUID);
            TestAttachedObject(attachments[1], AttachmentPoint.Chest, npc.UUID);

            // Attached objects on the same point must have different FromItemIDs to be shown to other avatars, at least
            // on Singularity 1.8.5.  Otherwise, only one (the first ObjectUpdate sent) appears.
            Assert.AreNotEqual(attachments[0].FromItemID, attachments[1].FromItemID);
        }

        private void TestAttachedObject(SceneObjectGroup attSo, AttachmentPoint attPoint, UUID ownerId)
        {
            Assert.That(attSo.AttachmentPoint, Is.EqualTo((byte)attPoint));
            Assert.That(attSo.IsAttachment);
            Assert.That(attSo.UsesPhysics, Is.False);
            Assert.That(attSo.IsTemporary, Is.False);
            Assert.That(attSo.OwnerID, Is.EqualTo(ownerId));
        }

        [Test]
        public void TestLoadAppearance()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            SetUpScene();

            UUID userId = TestHelpers.ParseTail(0x1);
            UserAccountHelpers.CreateUserWithInventory(m_scene, userId);
            ScenePresence sp = SceneHelpers.AddScenePresence(m_scene, userId);

            UUID npcId = m_npcMod.CreateNPC("John", "Smith", new Vector3(128, 128, 30), UUID.Zero, true, m_scene, sp.Appearance);

            // Now add the attachment to the original avatar and use that to load a new appearance
            // TODO: Could also run tests loading from a notecard though this isn't much different for our purposes here
            UUID attItemId = TestHelpers.ParseTail(0x2);
            UUID attAssetId = TestHelpers.ParseTail(0x3);
            string attName = "att";

            UserInventoryHelpers.CreateInventoryItem(m_scene, attName, attItemId, attAssetId, sp.UUID, InventoryType.Object);

            m_attMod.RezSingleAttachmentFromInventory(sp, attItemId, (uint)AttachmentPoint.Chest);

            m_npcMod.SetNPCAppearance(npcId, sp.Appearance, m_scene);

            ScenePresence npc = m_scene.GetScenePresence(npcId);

            // Check scene presence status
            Assert.That(npc.HasAttachments(), Is.True);
            List<SceneObjectGroup> attachments = npc.GetAttachments();
            Assert.That(attachments.Count, Is.EqualTo(1));
            SceneObjectGroup attSo = attachments[0];

            // Just for now, we won't test the name since this is (wrongly) the asset part name rather than the item
            // name.  TODO: Do need to fix ultimately since the item may be renamed before being passed on to an NPC.
//            Assert.That(attSo.Name, Is.EqualTo(attName));

            Assert.That(attSo.AttachmentPoint, Is.EqualTo((byte)AttachmentPoint.Chest));
            Assert.That(attSo.IsAttachment);
            Assert.That(attSo.UsesPhysics, Is.False);
            Assert.That(attSo.IsTemporary, Is.False);
            Assert.That(attSo.OwnerID, Is.EqualTo(npc.UUID));
        }

        [Test]
        public void TestMove()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            SetUpScene();

            ScenePresence sp = SceneHelpers.AddScenePresence(m_scene, TestHelpers.ParseTail(0x1));
//            ScenePresence originalAvatar = scene.GetScenePresence(originalClient.AgentId);

            Vector3 startPos = new Vector3(128, 128, 30);
            UUID npcId = m_npcMod.CreateNPC("John", "Smith", startPos, UUID.Zero, true, m_scene, sp.Appearance);

            ScenePresence npc = m_scene.GetScenePresence(npcId);
            Assert.That(npc.AbsolutePosition, Is.EqualTo(startPos));

            // For now, we'll make the scene presence fly to simplify this test, but this needs to change.
            npc.Flying = true;

            m_scene.Update(1);
            Assert.That(npc.AbsolutePosition, Is.EqualTo(startPos));

            Vector3 targetPos = startPos + new Vector3(0, 10, 0);
            m_npcMod.MoveToTarget(npc.UUID, m_scene, targetPos, false, false, false);

            Assert.That(npc.AbsolutePosition, Is.EqualTo(startPos));
            //Assert.That(npc.Rotation, Is.EqualTo(new Quaternion(0, 0, 0.7071068f, 0.7071068f)));
            Assert.That(
                npc.Rotation, new QuaternionToleranceConstraint(new Quaternion(0, 0, 0.7071068f, 0.7071068f), 0.000001));

            m_scene.Update(1);

            // We should really check the exact figure.
            Assert.That(npc.AbsolutePosition.X, Is.EqualTo(startPos.X));
            Assert.That(npc.AbsolutePosition.Y, Is.GreaterThan(startPos.Y));
            Assert.That(npc.AbsolutePosition.Z, Is.EqualTo(startPos.Z));
            Assert.That(npc.AbsolutePosition.Z, Is.LessThan(targetPos.X));

            m_scene.Update(10);

            double distanceToTarget = Util.GetDistanceTo(npc.AbsolutePosition, targetPos);
            Assert.That(distanceToTarget, Is.LessThan(1), "NPC not within 1 unit of target position on first move");
            Assert.That(npc.AbsolutePosition, Is.EqualTo(targetPos));
            Assert.That(npc.AgentControlFlags, Is.EqualTo((uint)AgentManager.ControlFlags.NONE));

            // Try a second movement
            startPos = npc.AbsolutePosition;
            targetPos = startPos + new Vector3(10, 0, 0);
            m_npcMod.MoveToTarget(npc.UUID, m_scene, targetPos, false, false, false);

            Assert.That(npc.AbsolutePosition, Is.EqualTo(startPos));
//            Assert.That(npc.Rotation, Is.EqualTo(new Quaternion(0, 0, 0, 1)));
            Assert.That(
                npc.Rotation, new QuaternionToleranceConstraint(new Quaternion(0, 0, 0, 1), 0.000001));

            m_scene.Update(1);

            // We should really check the exact figure.
            Assert.That(npc.AbsolutePosition.X, Is.GreaterThan(startPos.X));
            Assert.That(npc.AbsolutePosition.X, Is.LessThan(targetPos.X));
            Assert.That(npc.AbsolutePosition.Y, Is.EqualTo(startPos.Y));
            Assert.That(npc.AbsolutePosition.Z, Is.EqualTo(startPos.Z));

            m_scene.Update(10);

            distanceToTarget = Util.GetDistanceTo(npc.AbsolutePosition, targetPos);
            Assert.That(distanceToTarget, Is.LessThan(1), "NPC not within 1 unit of target position on second move");
            Assert.That(npc.AbsolutePosition, Is.EqualTo(targetPos));
        }

        [Test]
        public void TestMoveInVarRegion()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            SetUpScene(512, 512);

            ScenePresence sp = SceneHelpers.AddScenePresence(m_scene, TestHelpers.ParseTail(0x1));
//            ScenePresence originalAvatar = scene.GetScenePresence(originalClient.AgentId);

            Vector3 startPos = new Vector3(128, 246, 30);
            UUID npcId = m_npcMod.CreateNPC("John", "Smith", startPos, UUID.Zero, true, m_scene, sp.Appearance);

            ScenePresence npc = m_scene.GetScenePresence(npcId);
            Assert.That(npc.AbsolutePosition, Is.EqualTo(startPos));

            // For now, we'll make the scene presence fly to simplify this test, but this needs to change.
            npc.Flying = true;

            m_scene.Update(1);
            Assert.That(npc.AbsolutePosition, Is.EqualTo(startPos));

            Vector3 targetPos = startPos + new Vector3(0, 20, 0);
            m_npcMod.MoveToTarget(npc.UUID, m_scene, targetPos, false, false, false);

            Assert.That(npc.AbsolutePosition, Is.EqualTo(startPos));
            //Assert.That(npc.Rotation, Is.EqualTo(new Quaternion(0, 0, 0.7071068f, 0.7071068f)));
            Assert.That(
                npc.Rotation, new QuaternionToleranceConstraint(new Quaternion(0, 0, 0.7071068f, 0.7071068f), 0.000001));

            m_scene.Update(1);

            // We should really check the exact figure.
            Assert.That(npc.AbsolutePosition.X, Is.EqualTo(startPos.X));
            Assert.That(npc.AbsolutePosition.Y, Is.GreaterThan(startPos.Y));
            Assert.That(npc.AbsolutePosition.Z, Is.EqualTo(startPos.Z));
            Assert.That(npc.AbsolutePosition.Z, Is.LessThan(targetPos.X));

            for (int i = 0; i < 20; i++)
            {
                m_scene.Update(1);
//                Console.WriteLine("pos: {0}", npc.AbsolutePosition);
            }

            double distanceToTarget = Util.GetDistanceTo(npc.AbsolutePosition, targetPos);
            Assert.That(distanceToTarget, Is.LessThan(1), "NPC not within 1 unit of target position on first move");
            Assert.That(npc.AbsolutePosition, Is.EqualTo(targetPos));
            Assert.That(npc.AgentControlFlags, Is.EqualTo((uint)AgentManager.ControlFlags.NONE));
        }

        [Test]
        public void TestSitAndStandWithSitTarget()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            SetUpScene();

            ScenePresence sp = SceneHelpers.AddScenePresence(m_scene, TestHelpers.ParseTail(0x1));

            Vector3 startPos = new Vector3(128, 128, 30);
            UUID npcId = m_npcMod.CreateNPC("John", "Smith", startPos, UUID.Zero, true, m_scene, sp.Appearance);

            ScenePresence npc = m_scene.GetScenePresence(npcId);
            SceneObjectPart part = SceneHelpers.AddSceneObject(m_scene).RootPart;

            part.SitTargetPosition = new Vector3(0, 0, 1);
            m_npcMod.Sit(npc.UUID, part.UUID, m_scene);

            Assert.That(part.SitTargetAvatar, Is.EqualTo(npcId));
            Assert.That(npc.ParentID, Is.EqualTo(part.LocalId));
//            Assert.That(
//                npc.AbsolutePosition,
//                Is.EqualTo(part.AbsolutePosition + part.SitTargetPosition + ScenePresence.SIT_TARGET_ADJUSTMENT));

            m_npcMod.Stand(npc.UUID, m_scene);

            Assert.That(part.SitTargetAvatar, Is.EqualTo(UUID.Zero));
            Assert.That(npc.ParentID, Is.EqualTo(0));
        }

        [Test]
        public void TestSitAndStandWithNoSitTarget()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            SetUpScene();

            ScenePresence sp = SceneHelpers.AddScenePresence(m_scene, TestHelpers.ParseTail(0x1));

            // FIXME: To get this to work for now, we are going to place the npc right next to the target so that
            // the autopilot doesn't trigger
            Vector3 startPos = new Vector3(1, 1, 1);

            UUID npcId = m_npcMod.CreateNPC("John", "Smith", startPos, UUID.Zero, true, m_scene, sp.Appearance);

            ScenePresence npc = m_scene.GetScenePresence(npcId);
            SceneObjectPart part = SceneHelpers.AddSceneObject(m_scene).RootPart;

            m_npcMod.Sit(npc.UUID, part.UUID, m_scene);

            Assert.That(part.SitTargetAvatar, Is.EqualTo(UUID.Zero));
            Assert.That(npc.ParentID, Is.EqualTo(part.LocalId));

            // We should really be using the NPC size but this would mean preserving the physics actor since it is
            // removed on sit.
            Assert.That(
                npc.AbsolutePosition,
                Is.EqualTo(part.AbsolutePosition + new Vector3(0, 0, sp.PhysicsActor.Size.Z / 2)));

            m_npcMod.Stand(npc.UUID, m_scene);

            Assert.That(part.SitTargetAvatar, Is.EqualTo(UUID.Zero));
            Assert.That(npc.ParentID, Is.EqualTo(0));
        }
    }
}
