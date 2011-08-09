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
using System.Text;
using Nini.Config;
using NUnit.Framework;
using OpenMetaverse;
using OpenMetaverse.Assets;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Region.CoreModules.Avatar.AvatarFactory;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.ScriptEngine.Shared;
using OpenSim.Region.ScriptEngine.Shared.Api;
using OpenSim.Services.Interfaces;
using OpenSim.Tests.Common;
using OpenSim.Tests.Common.Mock;

namespace OpenSim.Region.ScriptEngine.Shared.Tests
{
    /// <summary>
    /// Tests for OSSL_Api
    /// </summary>
    [TestFixture]
    public class OSSL_ApiAppearanceTest
    {
        [Test]
        public void TestOsOwnerSaveAppearance()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            IConfigSource initConfigSource = new IniConfigSource();
            IConfig config = initConfigSource.AddConfig("XEngine");
            config.Set("Enabled", "true");
            config.Set("AllowOSFunctions", "true");
            config.Set("OSFunctionThreatLevel", "Severe");

            UUID userId = TestHelpers.ParseTail(0x1);
            float newHeight = 1.9f;

            Scene scene = SceneHelpers.SetupScene();
            SceneHelpers.SetupSceneModules(scene, new AvatarFactoryModule());
            ScenePresence sp = SceneHelpers.AddScenePresence(scene, userId);
            sp.Appearance.AvatarHeight = newHeight;
            SceneObjectGroup so = SceneHelpers.CreateSceneObject(1, userId);
            SceneObjectPart part = so.RootPart;
            scene.AddSceneObject(so);

            XEngine.XEngine engine = new XEngine.XEngine();
            engine.Initialise(initConfigSource);
            engine.AddRegion(scene);

            OSSL_Api osslApi = new OSSL_Api();
            osslApi.Initialize(engine, part, part.LocalId, part.UUID);

            string notecardName = "appearanceNc";

            osslApi.osOwnerSaveAppearance(notecardName);

            IList<TaskInventoryItem> items = part.Inventory.GetInventoryItems(notecardName);
            Assert.That(items.Count, Is.EqualTo(1));

            TaskInventoryItem ncItem = items[0];
            Assert.That(ncItem.Name, Is.EqualTo(notecardName));

            AssetBase ncAsset = scene.AssetService.Get(ncItem.AssetID.ToString());
            Assert.That(ncAsset, Is.Not.Null);

            AssetNotecard anc = new AssetNotecard(UUID.Zero, ncAsset.Data);
            anc.Decode();
            OSDMap appearanceOsd = (OSDMap)OSDParser.DeserializeLLSDXml(anc.BodyText);
            AvatarAppearance savedAppearance = new AvatarAppearance();
            savedAppearance.Unpack(appearanceOsd);

            Assert.That(savedAppearance.AvatarHeight, Is.EqualTo(sp.Appearance.AvatarHeight));
        }
    }
}