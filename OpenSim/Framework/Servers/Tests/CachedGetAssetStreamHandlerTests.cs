using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using OpenSim.Data;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Services.Interfaces;
using OpenSim.Tests.Common;
using OpenSim.Tests.Common.Mock;
using OpenSim.Tests.Common.Setup;

namespace OpenSim.Framework.Servers.Tests
{
    [TestFixture]
    public class CachedGetAssetStreamHandlerTests
    {
        private const string ASSETS_PATH = "/assets";

        [Test]
        public void TestConstructor()
        {
            TestHelper.InMethod();

            CachedGetAssetStreamHandler handler = new CachedGetAssetStreamHandler(null);
        }

        [Test]
        public void TestGetParams()
        {
            TestHelper.InMethod();

            CachedGetAssetStreamHandler handler = new CachedGetAssetStreamHandler(null);
            BaseRequestHandlerTestHelper.BaseTestGetParams(handler, ASSETS_PATH);
        }

        [Test]
        public void TestSplitParams()
        {
            TestHelper.InMethod();

            CachedGetAssetStreamHandler handler = new CachedGetAssetStreamHandler(null);
            BaseRequestHandlerTestHelper.BaseTestSplitParams(handler, ASSETS_PATH);
        }

        [Test]
        public void TestHandleNoParams()
        {
            TestHelper.InMethod();

            CachedGetAssetStreamHandler handler = new CachedGetAssetStreamHandler(null);

            BaseRequestHandlerTestHelper.BaseTestHandleNoParams(handler, ASSETS_PATH);
        }

        [Test]
        public void TestHandleMalformedGuid()
        {
            TestHelper.InMethod();

            CachedGetAssetStreamHandler handler = new CachedGetAssetStreamHandler(null);

            BaseRequestHandlerTestHelper.BaseTestHandleMalformedGuid(handler, ASSETS_PATH);
        }

        [Test]
        public void TestHandleFetchMissingAsset()
        {
            IAssetCache assetCache = new TestAssetCache();
            CachedGetAssetStreamHandler handler = new CachedGetAssetStreamHandler(assetCache);

            GetAssetStreamHandlerTestHelpers.BaseFetchMissingAsset(handler);
        }

        [Test]
        public void TestHandleFetchExistingAssetData()
        {
            CachedGetAssetStreamHandler handler;
            OSHttpResponse response;
            AssetBase asset = CreateTestEnvironment(out handler, out response);

            GetAssetStreamHandlerTestHelpers.BaseFetchExistingAssetDataTest(asset, handler, response);
        }

        private static AssetBase CreateTestEnvironment(out CachedGetAssetStreamHandler handler, out OSHttpResponse response)
        {
            AssetBase asset = GetAssetStreamHandlerTestHelpers.CreateCommonTestResources(out response);

            IAssetCache assetDataPlugin = new TestAssetCache();
            handler = new CachedGetAssetStreamHandler(assetDataPlugin);

            assetDataPlugin.AddAsset(asset);
            return asset;
        }
    }
}
