using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using HttpServer;
using NUnit.Framework;
using OpenSim.Data;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Tests.Common;
using OpenSim.Tests.Common.Mock;
using OpenSim.Tests.Common.Setup;

namespace OpenSim.Framework.Servers.Tests
{
    [TestFixture]
    public class GetAssetStreamHandlerTests
    {
        private const string ASSETS_PATH = "/assets";

        [Test]
        public void TestConstructor()
        {
            TestHelper.InMethod();

            GetAssetStreamHandler handler = new GetAssetStreamHandler( null );
        }

        [Test]
        public void TestGetParams()
        {
            TestHelper.InMethod();

            GetAssetStreamHandler handler = new GetAssetStreamHandler(null);
            BaseRequestHandlerTestHelper.BaseTestGetParams(handler, ASSETS_PATH);
        }

        [Test]
        public void TestSplitParams()
        {
            TestHelper.InMethod();

            GetAssetStreamHandler handler = new GetAssetStreamHandler(null);
            BaseRequestHandlerTestHelper.BaseTestSplitParams(handler, ASSETS_PATH);
        }

        [Test]
        public void TestHandleNoParams()
        {
            TestHelper.InMethod();

            GetAssetStreamHandler handler = new GetAssetStreamHandler(null);

            BaseRequestHandlerTestHelper.BaseTestHandleNoParams(handler, ASSETS_PATH);
        }

        [Test]
        public void TestHandleMalformedGuid()
        {
            TestHelper.InMethod();

            GetAssetStreamHandler handler = new GetAssetStreamHandler(null);

            BaseRequestHandlerTestHelper.BaseTestHandleMalformedGuid(handler, ASSETS_PATH);
        }

        [Test]
        public void TestHandleFetchMissingAsset()
        {
            IAssetDataPlugin assetDataPlugin = new TestAssetDataPlugin();
            GetAssetStreamHandler handler = new GetAssetStreamHandler(assetDataPlugin);

            GetAssetStreamHandlerTestHelpers.BaseFetchMissingAsset(handler);
        }

        [Test]
        public void TestHandleFetchExistingAssetData()
        {
            GetAssetStreamHandler handler;
            OSHttpResponse response;
            AssetBase asset = CreateTestEnvironment(out handler, out response);

            GetAssetStreamHandlerTestHelpers.BaseFetchExistingAssetDataTest(asset, handler, response);
        }

        [Test]
        public void TestHandleFetchExistingAssetXml()
        {
            GetAssetStreamHandler handler;
            OSHttpResponse response;
            AssetBase asset = CreateTestEnvironment(out handler, out response);

            GetAssetStreamHandlerTestHelpers.BaseFetchExistingAssetXmlTest(asset, handler, response);
        }

        private static AssetBase CreateTestEnvironment(out GetAssetStreamHandler handler, out OSHttpResponse response)
        {
            AssetBase asset = GetAssetStreamHandlerTestHelpers.CreateCommonTestResources(out response);

            IAssetDataPlugin assetDataPlugin = new TestAssetDataPlugin();
            handler = new GetAssetStreamHandler(assetDataPlugin);

            assetDataPlugin.CreateAsset(asset);
            return asset;
        }
    }
}
