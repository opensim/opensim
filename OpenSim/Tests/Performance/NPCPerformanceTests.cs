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
using System.Diagnostics;
using System.Reflection;
using log4net;
using Nini.Config;
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Region.CoreModules.Avatar.Attachments;
using OpenSim.Region.CoreModules.Avatar.AvatarFactory;
using OpenSim.Region.CoreModules.Framework.InventoryAccess;
using OpenSim.Region.CoreModules.Framework.UserManagement;
using OpenSim.Region.CoreModules.ServiceConnectorsOut.Avatar;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.OptionalModules.World.NPC;
using OpenSim.Services.AvatarService;
using OpenSim.Tests.Common;

namespace OpenSim.Tests.Performance
{
    /// <summary>
    /// NPC performance tests
    /// </summary>
    /// <remarks>
    /// Don't rely on the numbers given by these tests - they will vary a lot depending on what is already cached,
    /// how much memory is free, etc.  In some cases, later larger tests will apparently take less time than smaller
    /// earlier tests.
    /// </remarks>
    [TestFixture]
    public class NPCPerformanceTests : OpenSimTestCase
    {
        private TestScene scene;
        private AvatarFactoryModule afm;
        private UserManagementModule umm;
        private AttachmentsModule am;

        [TestFixtureSetUp]
        public void FixtureInit()
        {
            // Don't allow tests to be bamboozled by asynchronous events.  Execute everything on the same thread.
            Util.FireAndForgetMethod = FireAndForgetMethod.None;
        }

        [TestFixtureTearDown]
        public void TearDown()
        {
            scene.Close();
            scene = null;
            GC.Collect();
            GC.WaitForPendingFinalizers();

            // We must set this back afterwards, otherwise later tests will fail since they're expecting multiple
            // threads.  Possibly, later tests should be rewritten not to worry about such things.
            Util.FireAndForgetMethod = Util.DefaultFireAndForgetMethod;
        }

        [SetUp]
        public void Init()
        {
            IConfigSource config = new IniConfigSource();
            config.AddConfig("NPC");
            config.Configs["NPC"].Set("Enabled", "true");
            config.AddConfig("Modules");
            config.Configs["Modules"].Set("InventoryAccessModule", "BasicInventoryAccessModule");

            afm = new AvatarFactoryModule();
            umm = new UserManagementModule();
            am = new AttachmentsModule();

            scene = new SceneHelpers().SetupScene();
            SceneHelpers.SetupSceneModules(scene, config, afm, umm, am, new BasicInventoryAccessModule(), new NPCModule());
        }

        [Test]
        public void Test_0001_AddRemove100NPCs()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            TestAddRemoveNPCs(100);
        }

        [Test]
        public void Test_0002_AddRemove1000NPCs()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            TestAddRemoveNPCs(1000);
        }

        [Test]
        public void Test_0003_AddRemove2000NPCs()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            TestAddRemoveNPCs(2000);
        }

        private void TestAddRemoveNPCs(int numberOfNpcs)
        {
            ScenePresence sp = SceneHelpers.AddScenePresence(scene, TestHelpers.ParseTail(0x1));
//            ScenePresence originalAvatar = scene.GetScenePresence(originalClient.AgentId);

            // 8 is the index of the first baked texture in AvatarAppearance
            UUID originalFace8TextureId = TestHelpers.ParseTail(0x10);
            Primitive.TextureEntry originalTe = new Primitive.TextureEntry(UUID.Zero);
            Primitive.TextureEntryFace originalTef = originalTe.CreateFace(8);
            originalTef.TextureID = originalFace8TextureId;

            // We also need to add the texture to the asset service, otherwise the AvatarFactoryModule will tell
            // ScenePresence.SendInitialData() to reset our entire appearance.
            scene.AssetService.Store(AssetHelpers.CreateNotecardAsset(originalFace8TextureId));

/*
            afm.SetAppearance(sp, originalTe, null);

            INPCModule npcModule = scene.RequestModuleInterface<INPCModule>();

            List<UUID> npcs = new List<UUID>();

            long startGcMemory = GC.GetTotalMemory(true);
            Stopwatch sw = new Stopwatch();
            sw.Start();

            for (int i = 0; i < numberOfNpcs; i++)
            {
                npcs.Add(
                    npcModule.CreateNPC("John", "Smith", new Vector3(128, 128, 30), UUID.Zero, true, scene, sp.Appearance));
            }

            for (int i = 0; i < numberOfNpcs; i++)
            {
                Assert.That(npcs[i], Is.Not.Null);

                ScenePresence npc = scene.GetScenePresence(npcs[i]);
                Assert.That(npc, Is.Not.Null);
            }

            for (int i = 0; i < numberOfNpcs; i++)
            {
                Assert.That(npcModule.DeleteNPC(npcs[i], scene), Is.True);
                ScenePresence npc = scene.GetScenePresence(npcs[i]);
                Assert.That(npc, Is.Null);
            }

            sw.Stop();

            long endGcMemory = GC.GetTotalMemory(true);

            Console.WriteLine("Took {0} ms", sw.ElapsedMilliseconds);
            Console.WriteLine(
                "End {0} MB, Start {1} MB, Diff {2} MB",
                endGcMemory / 1024 / 1024,
                startGcMemory / 1024 / 1024,
                (endGcMemory - startGcMemory) / 1024 / 1024);
*/
        }
    }
}
