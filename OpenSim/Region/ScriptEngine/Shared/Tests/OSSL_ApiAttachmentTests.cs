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
using log4net;
using Nini.Config;
using NUnit.Framework;
using OpenMetaverse;
using OpenMetaverse.Assets;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Region.CoreModules.Avatar.Attachments;
using OpenSim.Region.CoreModules.Framework.InventoryAccess;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.ScriptEngine.Shared;
using OpenSim.Region.ScriptEngine.Shared.Api;
using OpenSim.Region.ScriptEngine.Shared.Instance;
using OpenSim.Services.Interfaces;
using OpenSim.Tests.Common;

namespace OpenSim.Region.ScriptEngine.Shared.Tests
{
    /// <summary>
    /// Tests for OSSL attachment functions
    /// </summary>
    /// <remarks>
    /// TODO: Add tests for all functions
    /// </remarks>
    [TestFixture]
    public class OSSL_ApiAttachmentTests : OpenSimTestCase
    {
        protected Scene m_scene;
        protected XEngine.XEngine m_engine;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            IConfigSource initConfigSource = new IniConfigSource();

            IConfig xengineConfig = initConfigSource.AddConfig("XEngine");
            xengineConfig.Set("Enabled", "true");
            xengineConfig.Set("AllowOSFunctions", "true");
            xengineConfig.Set("OSFunctionThreatLevel", "Severe");

            IConfig modulesConfig = initConfigSource.AddConfig("Modules");
            modulesConfig.Set("InventoryAccessModule", "BasicInventoryAccessModule");

            m_scene = new SceneHelpers().SetupScene();
            SceneHelpers.SetupSceneModules(
                m_scene, initConfigSource, new AttachmentsModule(), new BasicInventoryAccessModule());

            m_engine = new XEngine.XEngine();
            m_engine.Initialise(initConfigSource);
            m_engine.AddRegion(m_scene);
        }

        [Test]
        public void TestOsForceAttachToAvatarFromInventory()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            string taskInvObjItemName = "sphere";
            UUID taskInvObjItemId = UUID.Parse("00000000-0000-0000-0000-100000000000");
            AttachmentPoint attachPoint = AttachmentPoint.Chin;

            UserAccount ua1 = UserAccountHelpers.CreateUserWithInventory(m_scene, 0x1);
            ScenePresence sp = SceneHelpers.AddScenePresence(m_scene, ua1.PrincipalID);
            SceneObjectGroup inWorldObj = SceneHelpers.AddSceneObject(m_scene, "inWorldObj", ua1.PrincipalID);
            TaskInventoryItem scriptItem = TaskInventoryHelpers.AddScript(m_scene.AssetService, inWorldObj.RootPart);

            new LSL_Api().Initialize(m_engine, inWorldObj.RootPart, scriptItem);
            OSSL_Api osslApi = new OSSL_Api();
            osslApi.Initialize(m_engine, inWorldObj.RootPart, scriptItem);

//            SceneObjectGroup sog1 = SceneHelpers.CreateSceneObject(1, ua1.PrincipalID);

            // Create an object embedded inside the first
            TaskInventoryHelpers.AddSceneObject(m_scene.AssetService, inWorldObj.RootPart, taskInvObjItemName, taskInvObjItemId, ua1.PrincipalID);

            osslApi.osForceAttachToAvatarFromInventory(taskInvObjItemName, (int)attachPoint);

            // Check scene presence status
            Assert.That(sp.HasAttachments(), Is.True);
            List<SceneObjectGroup> attachments = sp.GetAttachments();
            Assert.That(attachments.Count, Is.EqualTo(1));
            SceneObjectGroup attSo = attachments[0];
            Assert.That(attSo.Name, Is.EqualTo(taskInvObjItemName));
            Assert.That(attSo.AttachmentPoint, Is.EqualTo((uint)attachPoint));
            Assert.That(attSo.IsAttachment);
            Assert.That(attSo.UsesPhysics, Is.False);
            Assert.That(attSo.IsTemporary, Is.False);

            // Check appearance status
            List<AvatarAttachment> attachmentsInAppearance = sp.Appearance.GetAttachments();
            Assert.That(attachmentsInAppearance.Count, Is.EqualTo(1));
            Assert.That(sp.Appearance.GetAttachpoint(attachmentsInAppearance[0].ItemID), Is.EqualTo((uint)attachPoint));
        }

        /// <summary>
        /// Make sure we can't force attach anything other than objects.
        /// </summary>
        [Test]
        public void TestOsForceAttachToAvatarFromInventoryNotObject()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            string taskInvObjItemName = "sphere";
            UUID taskInvObjItemId = UUID.Parse("00000000-0000-0000-0000-100000000000");
            AttachmentPoint attachPoint = AttachmentPoint.Chin;

            UserAccount ua1 = UserAccountHelpers.CreateUserWithInventory(m_scene, 0x1);
            ScenePresence sp = SceneHelpers.AddScenePresence(m_scene, ua1.PrincipalID);
            SceneObjectGroup inWorldObj = SceneHelpers.AddSceneObject(m_scene, "inWorldObj", ua1.PrincipalID);
            TaskInventoryItem scriptItem = TaskInventoryHelpers.AddScript(m_scene.AssetService, inWorldObj.RootPart);

            new LSL_Api().Initialize(m_engine, inWorldObj.RootPart, scriptItem);
            OSSL_Api osslApi = new OSSL_Api();
            osslApi.Initialize(m_engine, inWorldObj.RootPart, scriptItem);

            // Create an object embedded inside the first
            TaskInventoryHelpers.AddNotecard(
                m_scene.AssetService, inWorldObj.RootPart, taskInvObjItemName, taskInvObjItemId, TestHelpers.ParseTail(0x900), "Hello World!");

            bool exceptionCaught = false;

            try
            {
                osslApi.osForceAttachToAvatarFromInventory(taskInvObjItemName, (int)attachPoint);
            }
            catch (Exception)
            {
                exceptionCaught = true;
            }

            Assert.That(exceptionCaught, Is.True);

            // Check scene presence status
            Assert.That(sp.HasAttachments(), Is.False);
            List<SceneObjectGroup> attachments = sp.GetAttachments();
            Assert.That(attachments.Count, Is.EqualTo(0));

            // Check appearance status
            List<AvatarAttachment> attachmentsInAppearance = sp.Appearance.GetAttachments();
            Assert.That(attachmentsInAppearance.Count, Is.EqualTo(0));
        }

        [Test]
        public void TestOsForceAttachToOtherAvatarFromInventory()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            string taskInvObjItemName = "sphere";
            UUID taskInvObjItemId = UUID.Parse("00000000-0000-0000-0000-100000000000");
            AttachmentPoint attachPoint = AttachmentPoint.Chin;

            UserAccount ua1 = UserAccountHelpers.CreateUserWithInventory(m_scene, "user", "one", 0x1, "pass");
            UserAccount ua2 = UserAccountHelpers.CreateUserWithInventory(m_scene, "user", "two", 0x2, "pass");

            ScenePresence sp = SceneHelpers.AddScenePresence(m_scene, ua1);
            SceneObjectGroup inWorldObj = SceneHelpers.AddSceneObject(m_scene, "inWorldObj", ua1.PrincipalID);
            TaskInventoryItem scriptItem = TaskInventoryHelpers.AddScript(m_scene.AssetService, inWorldObj.RootPart);

            new LSL_Api().Initialize(m_engine, inWorldObj.RootPart, scriptItem);
            OSSL_Api osslApi = new OSSL_Api();
            osslApi.Initialize(m_engine, inWorldObj.RootPart, scriptItem);

            // Create an object embedded inside the first
            TaskInventoryHelpers.AddSceneObject(
                m_scene.AssetService, inWorldObj.RootPart, taskInvObjItemName, taskInvObjItemId, ua1.PrincipalID);

            ScenePresence sp2 = SceneHelpers.AddScenePresence(m_scene, ua2);

            osslApi.osForceAttachToOtherAvatarFromInventory(sp2.UUID.ToString(), taskInvObjItemName, (int)attachPoint);

            // Check scene presence status
            Assert.That(sp.HasAttachments(), Is.False);
            List<SceneObjectGroup> attachments = sp.GetAttachments();
            Assert.That(attachments.Count, Is.EqualTo(0));

            Assert.That(sp2.HasAttachments(), Is.True);
            List<SceneObjectGroup> attachments2 = sp2.GetAttachments();
            Assert.That(attachments2.Count, Is.EqualTo(1));
            SceneObjectGroup attSo = attachments2[0];
            Assert.That(attSo.Name, Is.EqualTo(taskInvObjItemName));
            Assert.That(attSo.OwnerID, Is.EqualTo(ua2.PrincipalID));
            Assert.That(attSo.AttachmentPoint, Is.EqualTo((uint)attachPoint));
            Assert.That(attSo.IsAttachment);
            Assert.That(attSo.UsesPhysics, Is.False);
            Assert.That(attSo.IsTemporary, Is.False);

            // Check appearance status
            List<AvatarAttachment> attachmentsInAppearance = sp.Appearance.GetAttachments();
            Assert.That(attachmentsInAppearance.Count, Is.EqualTo(0));

            List<AvatarAttachment> attachmentsInAppearance2 = sp2.Appearance.GetAttachments();
            Assert.That(attachmentsInAppearance2.Count, Is.EqualTo(1));
            Assert.That(sp2.Appearance.GetAttachpoint(attachmentsInAppearance2[0].ItemID), Is.EqualTo((uint)attachPoint));
        }
    }
}
