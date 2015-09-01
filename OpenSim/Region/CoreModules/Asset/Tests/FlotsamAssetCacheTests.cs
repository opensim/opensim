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
using System.Reflection;
using System.Threading;
using log4net.Config;
using Nini.Config;
using NUnit.Framework;
using OpenMetaverse;
using OpenMetaverse.Assets;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Scenes.Serialization;
using OpenSim.Tests.Common;

namespace OpenSim.Region.CoreModules.Asset.Tests
{
    /// <summary>
    /// At the moment we're only test the in-memory part of the FlotsamAssetCache.  This is a considerable weakness.
    /// </summary>
    [TestFixture]
    public class FlotsamAssetCacheTests : OpenSimTestCase
    {
        protected TestScene m_scene;
        protected FlotsamAssetCache m_cache;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            IConfigSource config = new IniConfigSource();

            config.AddConfig("Modules");
            config.Configs["Modules"].Set("AssetCaching", "FlotsamAssetCache");
            config.AddConfig("AssetCache");
            config.Configs["AssetCache"].Set("FileCacheEnabled", "false");
            config.Configs["AssetCache"].Set("MemoryCacheEnabled", "true");

            m_cache = new FlotsamAssetCache();
            m_scene = new SceneHelpers().SetupScene();
            SceneHelpers.SetupSceneModules(m_scene, config, m_cache);
        }

        [Test]
        public void TestCacheAsset()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            AssetBase asset = AssetHelpers.CreateNotecardAsset();
            asset.ID = TestHelpers.ParseTail(0x1).ToString();

            // Check we don't get anything before the asset is put in the cache
            AssetBase retrievedAsset = m_cache.Get(asset.ID.ToString());
            Assert.That(retrievedAsset, Is.Null);

            m_cache.Store(asset);

            // Check that asset is now in cache
            retrievedAsset = m_cache.Get(asset.ID.ToString());
            Assert.That(retrievedAsset, Is.Not.Null);
            Assert.That(retrievedAsset.ID, Is.EqualTo(asset.ID));
        }

        [Test]
        public void TestExpireAsset()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            AssetBase asset = AssetHelpers.CreateNotecardAsset();
            asset.ID = TestHelpers.ParseTail(0x2).ToString();

            m_cache.Store(asset);

            m_cache.Expire(asset.ID);

            AssetBase retrievedAsset = m_cache.Get(asset.ID.ToString());
            Assert.That(retrievedAsset, Is.Null);
        }

        [Test]
        public void TestClearCache()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();
            
            AssetBase asset = AssetHelpers.CreateNotecardAsset();
            asset.ID = TestHelpers.ParseTail(0x2).ToString();

            m_cache.Store(asset);

            m_cache.Clear();

            AssetBase retrievedAsset = m_cache.Get(asset.ID.ToString());
            Assert.That(retrievedAsset, Is.Null);
        }
    }
}