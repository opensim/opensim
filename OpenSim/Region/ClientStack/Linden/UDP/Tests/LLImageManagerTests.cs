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
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;
using log4net.Config;
using Nini.Config;
using NUnit.Framework;
using OpenMetaverse;
using OpenMetaverse.Packets;
using OpenSim.Framework;
using OpenSim.Region.CoreModules.Agent.TextureSender;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Tests.Common;

namespace OpenSim.Region.ClientStack.LindenUDP.Tests
{
    [TestFixture]
    public class LLImageManagerTests : OpenSimTestCase
    {
        private AssetBase m_testImageAsset;
        private Scene scene;
        private LLImageManager llim;
        private TestClient tc;

        [TestFixtureSetUp]
        public void FixtureInit()
        {
            // Don't allow tests to be bamboozled by asynchronous events.  Execute everything on the same thread.
            Util.FireAndForgetMethod = FireAndForgetMethod.None;

            using (
                Stream resource
                    = GetType().Assembly.GetManifestResourceStream(
                        "OpenSim.Region.ClientStack.LindenUDP.Tests.Resources.4-tile2.jp2"))
            {
                using (BinaryReader br = new BinaryReader(resource))
                {
                    m_testImageAsset
                        = new AssetBase(
                            TestHelpers.ParseTail(0x1),
                            "Test Image",
                            (sbyte)AssetType.Texture,
                            TestHelpers.ParseTail(0x2).ToString());

                    m_testImageAsset.Data = br.ReadBytes(99999999);
                }
            }
        }

        [TestFixtureTearDown]
        public void TearDown()
        {
            // We must set this back afterwards, otherwise later tests will fail since they're expecting multiple
            // threads.  Possibly, later tests should be rewritten not to worry about such things.
            Util.FireAndForgetMethod = Util.DefaultFireAndForgetMethod;
        }

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            UUID userId = TestHelpers.ParseTail(0x3);

            J2KDecoderModule j2kdm = new J2KDecoderModule();

            SceneHelpers sceneHelpers = new SceneHelpers();
            scene = sceneHelpers.SetupScene();
            SceneHelpers.SetupSceneModules(scene, j2kdm);

            tc = new TestClient(SceneHelpers.GenerateAgentData(userId), scene);
            llim = new LLImageManager(tc, scene.AssetService, j2kdm);
        }

        [Test]
        public void TestSendImage()
        {
            TestHelpers.InMethod();
//            XmlConfigurator.Configure();

            scene.AssetService.Store(m_testImageAsset);

            TextureRequestArgs args = new TextureRequestArgs();
            args.RequestedAssetID = m_testImageAsset.FullID;
            args.DiscardLevel = 0;
            args.PacketNumber = 1;
            args.Priority = 5;
            args.requestSequence = 1;

            llim.EnqueueReq(args);
            llim.ProcessImageQueue(20);

            Assert.That(tc.SentImageDataPackets.Count, Is.EqualTo(1));
        }

        [Test]
        public void TestDiscardImage()
        {
            TestHelpers.InMethod();
//            XmlConfigurator.Configure();

            scene.AssetService.Store(m_testImageAsset);

            TextureRequestArgs args = new TextureRequestArgs();
            args.RequestedAssetID = m_testImageAsset.FullID;
            args.DiscardLevel = 0;
            args.PacketNumber = 1;
            args.Priority = 5;
            args.requestSequence = 1;
            llim.EnqueueReq(args);

            // Now create a discard request
            TextureRequestArgs discardArgs = new TextureRequestArgs();
            discardArgs.RequestedAssetID = m_testImageAsset.FullID;
            discardArgs.DiscardLevel = -1;
            discardArgs.PacketNumber = 1;
            discardArgs.Priority = 0;
            discardArgs.requestSequence = 2;
            llim.EnqueueReq(discardArgs);

            llim.ProcessImageQueue(20);

            Assert.That(tc.SentImageDataPackets.Count, Is.EqualTo(0));
        }

        [Test]
        public void TestMissingImage()
        {
            TestHelpers.InMethod();
//            XmlConfigurator.Configure();

            TextureRequestArgs args = new TextureRequestArgs();
            args.RequestedAssetID = m_testImageAsset.FullID;
            args.DiscardLevel = 0;
            args.PacketNumber = 1;
            args.Priority = 5;
            args.requestSequence = 1;

            llim.EnqueueReq(args);
            llim.ProcessImageQueue(20);

            Assert.That(tc.SentImageDataPackets.Count, Is.EqualTo(0));
            Assert.That(tc.SentImageNotInDatabasePackets.Count, Is.EqualTo(1));
        }
    }
}