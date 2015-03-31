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
using System.Threading;
using System.Xml;
using Nini.Config;
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.CoreModules.Framework.InventoryAccess;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.ScriptEngine.XEngine;
using OpenSim.Services.Interfaces;
using OpenSim.Tests.Common;

namespace OpenSim.Region.CoreModules.Framework.InventoryAccess.Tests
{
    [TestFixture]
    public class HGAssetMapperTests : OpenSimTestCase
    {
        [Test]
        public void TestPostAssetRewrite()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            XEngine xengine = new OpenSim.Region.ScriptEngine.XEngine.XEngine();
            xengine.DebugLevel = 1;

            IniConfigSource configSource = new IniConfigSource();

            IConfig startupConfig = configSource.AddConfig("Startup");
            startupConfig.Set("DefaultScriptEngine", "XEngine");

            IConfig xEngineConfig = configSource.AddConfig("XEngine");
            xEngineConfig.Set("Enabled", "true");
            xEngineConfig.Set("StartDelay", "0");
            xEngineConfig.Set("AppDomainLoading", "false");

            string homeUrl = "http://hg.HomeTestPostAssetRewriteGrid.com";
            string foreignUrl = "http://hg.ForeignTestPostAssetRewriteGrid.com";
            int soIdTail = 0x1;
            UUID assetId = TestHelpers.ParseTail(0x10);
            UUID userId = TestHelpers.ParseTail(0x100);
            UUID sceneId = TestHelpers.ParseTail(0x1000);
            string userFirstName = "TestPostAsset";
            string userLastName = "Rewrite";
            int soPartsCount = 3;

            Scene scene = new SceneHelpers().SetupScene("TestPostAssetRewriteScene", sceneId, 1000, 1000, configSource);
            SceneHelpers.SetupSceneModules(scene, configSource, xengine);
            scene.StartScripts();

            HGAssetMapper hgam = new HGAssetMapper(scene, homeUrl);
            UserAccount ua 
                = UserAccountHelpers.CreateUserWithInventory(scene, userFirstName, userLastName, userId, "password");

            SceneObjectGroup so = SceneHelpers.AddSceneObject(scene, soPartsCount, ua.PrincipalID, "part", soIdTail);
            RezScript(
                scene, so.UUID, "default { state_entry() { llSay(0, \"Hello World\"); } }", "item1", ua.PrincipalID);

            AssetBase asset = AssetHelpers.CreateAsset(assetId, so);
            asset.CreatorID = foreignUrl;
            hgam.PostAsset(foreignUrl, asset);

            // Check transformed asset.
            AssetBase ncAssetGet = scene.AssetService.Get(assetId.ToString());
            Assert.AreEqual(foreignUrl, ncAssetGet.CreatorID);
            string xmlData = Utils.BytesToString(ncAssetGet.Data);
            XmlDocument ncAssetGetXmlDoc = new XmlDocument();
            ncAssetGetXmlDoc.LoadXml(xmlData);

//            Console.WriteLine(ncAssetGetXmlDoc.OuterXml);

            XmlNodeList creatorDataNodes = ncAssetGetXmlDoc.GetElementsByTagName("CreatorData");

            Assert.AreEqual(soPartsCount, creatorDataNodes.Count);
            //Console.WriteLine("creatorDataNodes {0}", creatorDataNodes.Count);

            foreach (XmlNode creatorDataNode in creatorDataNodes)
            {
                Assert.AreEqual(
                    string.Format("{0};{1} {2}", homeUrl, ua.FirstName, ua.LastName), creatorDataNode.InnerText);
            }

            // Check that saved script nodes have attributes
            XmlNodeList savedScriptStateNodes = ncAssetGetXmlDoc.GetElementsByTagName("SavedScriptState");

            Assert.AreEqual(1, savedScriptStateNodes.Count);
            Assert.AreEqual(1, savedScriptStateNodes[0].Attributes.Count);
            XmlNode uuidAttribute = savedScriptStateNodes[0].Attributes.GetNamedItem("UUID");
            Assert.NotNull(uuidAttribute);
            // XXX: To check the actual UUID attribute we would have to do some work to retreive the UUID of the task
            // item created earlier. 
        }

        private void RezScript(Scene scene, UUID soId, string script, string itemName, UUID userId)
        {
            InventoryItemBase itemTemplate = new InventoryItemBase();
            //            itemTemplate.ID = itemId;
            itemTemplate.Name = itemName;
            itemTemplate.Folder = soId;
            itemTemplate.InvType = (int)InventoryType.LSL;

            // XXX: Ultimately it would be better to be able to directly manipulate the script engine to rez a script
            // immediately for tests rather than chunter through it's threaded mechanisms.
            AutoResetEvent chatEvent = new AutoResetEvent(false);

            scene.EventManager.OnChatFromWorld += (s, c) => 
            {
//                Console.WriteLine("Got chat [{0}]", c.Message);           
                chatEvent.Set();
            };

            scene.RezNewScript(userId, itemTemplate, script);

//            Console.WriteLine("HERE");
            Assert.IsTrue(chatEvent.WaitOne(60000), "Chat event in HGAssetMapperTests.RezScript not received");
        }
    }
}