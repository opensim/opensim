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
using System.IO;
using System.Linq;
using System.Threading;
using Nini.Config;
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.CoreModules.Avatar.Attachments;
using OpenSim.Region.CoreModules.Framework.InventoryAccess;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.ScriptEngine.XEngine;
using OpenSim.Services.Interfaces;
using OpenSim.Tests.Common;

namespace OpenSim.Region.ScriptEngine.Tests
{
    /*
    [TestFixture]
    public class XEnginePersistenceTests : OpenSimTestCase
    {
        private AutoResetEvent m_chatEvent = new AutoResetEvent(false);

        private void OnChatFromWorld(object sender, OSChatMessage oscm)
        {
            //            Console.WriteLine("Got chat [{0}]", oscm.Message);

            //            m_osChatMessageReceived = oscm;
            m_chatEvent.Set();
        }

        private void AddCommonConfig(IConfigSource config, List<object> modules)
        {
            config.AddConfig("Modules");
            config.Configs["Modules"].Set("InventoryAccessModule", "BasicInventoryAccessModule");

            AttachmentsModule attMod = new AttachmentsModule();
            attMod.DebugLevel = 1;
            modules.Add(attMod);
            modules.Add(new BasicInventoryAccessModule());
        }

        private void AddScriptingConfig(IConfigSource config, XEngine.XEngine xEngine, List<object> modules)
        {
            IConfig startupConfig = config.AddConfig("Startup");
            startupConfig.Set("DefaultScriptEngine", "XEngine");

            IConfig xEngineConfig = config.AddConfig("XEngine");
            xEngineConfig.Set("Enabled", "true");
            xEngineConfig.Set("StartDelay", "0");

            // These tests will not run with AppDomainLoading = true, at least on mono.  For unknown reasons, the call
            // to AssemblyResolver.OnAssemblyResolve fails.
            xEngineConfig.Set("AppDomainLoading", "false");

            modules.Add(xEngine);
        }

        private Scene CreateScriptingEnabledTestScene(XEngine.XEngine xEngine)
        {
            IConfigSource config = new IniConfigSource();
            List<object> modules = new List<object>();

            AddCommonConfig(config, modules);
            AddScriptingConfig(config, xEngine, modules);

            Scene scene
                = new SceneHelpers().SetupScene(
                    "attachments-test-scene", TestHelpers.ParseTail(999), 1000, 1000, config);
            SceneHelpers.SetupSceneModules(scene, config, modules.ToArray());

            scene.StartScripts();

            return scene;
        }

        [Test]
        public void TestScriptedAttachmentPersistence()
        {
            TestHelpers.InMethod();
//                        TestHelpers.EnableLogging();

            XEngine.XEngine xEngine = new XEngine.XEngine();
            Scene scene = CreateScriptingEnabledTestScene(xEngine);
            UserAccount ua1 = UserAccountHelpers.CreateUserWithInventory(scene, 0x1);
            ScenePresence sp = SceneHelpers.AddScenePresence(scene, ua1);

            SceneObjectGroup so = SceneHelpers.CreateSceneObject(1, sp.UUID, "att-name", 0x10);
            TaskInventoryHelpers.AddScript(
                scene.AssetService,
                so.RootPart,
                "scriptItem",
                "default { attach(key id) { if (id != NULL_KEY) { llSay(0, \"Hello World\"); } } }");

            InventoryItemBase userItem = UserInventoryHelpers.AddInventoryItem(scene, so, 0x100, 0x1000);

            // FIXME: Right now, we have to do a tricksy chat listen to make sure we know when the script is running.
            // In the future, we need to be able to do this programatically more predicably.
            scene.EventManager.OnChatFromWorld += OnChatFromWorld;

            SceneObjectGroup rezzedSo
                = scene.AttachmentsModule.RezSingleAttachmentFromInventory(sp, userItem.ID, (uint)AttachmentPoint.Chest);
            TaskInventoryItem rezzedScriptItem = rezzedSo.RootPart.Inventory.GetInventoryItem("scriptItem");

            // Wait for chat to signal rezzed script has been started.
            m_chatEvent.WaitOne(60000);

            // Force save
            xEngine.DoBackup(new Object[] { 0 });

//            Console.WriteLine("ItemID {0}", rezzedScriptItem.ItemID);
//
//            foreach (
//                string s in Directory.EnumerateFileSystemEntries(
//                    string.Format("ScriptEngines/{0}", scene.RegionInfo.RegionID)))
//                Console.WriteLine(s);

            Assert.IsFalse(
                File.Exists(
                    string.Format("ScriptEngines/{0}/{1}.state", scene.RegionInfo.RegionID, rezzedScriptItem.ItemID)));

            scene.AttachmentsModule.DetachSingleAttachmentToInv(sp, rezzedSo);
        }
    }
     */
}