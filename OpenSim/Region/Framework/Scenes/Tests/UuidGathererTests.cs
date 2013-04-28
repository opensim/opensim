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

using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using OpenSim.Tests.Common;
using OpenSim.Tests.Common.Mock;

namespace OpenSim.Region.Framework.Scenes.Tests
{
    [TestFixture]
    public class UuidGathererTests : OpenSimTestCase
    {
        protected IAssetService m_assetService;
        protected UuidGatherer m_uuidGatherer;
            
        [SetUp]
        public void Init()
        {
            // FIXME: We don't need a full scene here - it would be enough to set up the asset service.
            Scene scene = new SceneHelpers().SetupScene();
            m_assetService = scene.AssetService;
            m_uuidGatherer = new UuidGatherer(m_assetService);
        }

        [Test]
        public void TestCorruptAsset()
        {
            TestHelpers.InMethod();
            
            UUID corruptAssetUuid = UUID.Parse("00000000-0000-0000-0000-000000000666");
            AssetBase corruptAsset 
                = AssetHelpers.CreateAsset(corruptAssetUuid, AssetType.Notecard, "CORRUPT ASSET", UUID.Zero);
            m_assetService.Store(corruptAsset);

            IDictionary<UUID, AssetType> foundAssetUuids = new Dictionary<UUID, AssetType>();
            m_uuidGatherer.GatherAssetUuids(corruptAssetUuid, AssetType.Object, foundAssetUuids);

            // We count the uuid as gathered even if the asset itself is corrupt.
            Assert.That(foundAssetUuids.Count, Is.EqualTo(1));
        }
        
        /// <summary>
        /// Test requests made for non-existent assets while we're gathering
        /// </summary>
        [Test]
        public void TestMissingAsset()
        {
            TestHelpers.InMethod();
            
            UUID missingAssetUuid = UUID.Parse("00000000-0000-0000-0000-000000000666");
            IDictionary<UUID, AssetType> foundAssetUuids = new Dictionary<UUID, AssetType>();
            
            m_uuidGatherer.GatherAssetUuids(missingAssetUuid, AssetType.Object, foundAssetUuids);

            // We count the uuid as gathered even if the asset itself is missing.
            Assert.That(foundAssetUuids.Count, Is.EqualTo(1));
        }

        [Test]
        public void TestNotecardAsset()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            UUID ownerId = TestHelpers.ParseTail(0x10);
            UUID soAssetId = TestHelpers.ParseTail(0x20);
            UUID ncAssetId = TestHelpers.ParseTail(0x30);

            SceneObjectGroup so = SceneHelpers.CreateSceneObject(1, ownerId);
            AssetBase soAsset = AssetHelpers.CreateAsset(soAssetId, so);
            m_assetService.Store(soAsset);

            AssetBase ncAsset = AssetHelpers.CreateNotecardAsset(ncAssetId, soAssetId.ToString());
            m_assetService.Store(ncAsset);

            IDictionary<UUID, AssetType> foundAssetUuids = new Dictionary<UUID, AssetType>();
            m_uuidGatherer.GatherAssetUuids(ncAssetId, AssetType.Notecard, foundAssetUuids);

            // We count the uuid as gathered even if the asset itself is corrupt.
            Assert.That(foundAssetUuids.Count, Is.EqualTo(2));
            Assert.That(foundAssetUuids.ContainsKey(ncAssetId));
            Assert.That(foundAssetUuids.ContainsKey(soAssetId));
        }
    }
}
