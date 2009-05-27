using NUnit.Framework;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Server.Handlers.Asset;
using OpenSim.Services.Interfaces;
using OpenSim.Tests.Common;
using OpenSim.Tests.Common.Mock;
using OpenSim.Tests.Common.Setup;

namespace OpenSim.Server.Handlers.Tests.Asset
{
    [TestFixture]
    public class AssetServerGetHandlerTests
    {
        private const string ASSETS_PATH = "/assets";

        [Test]
        public void TestConstructor()
        {
            TestHelper.InMethod();

            AssetServerGetHandler handler = new  AssetServerGetHandler( null );
        }

        [Test]
        public void TestGetParams()
        {
            TestHelper.InMethod();

            AssetServerGetHandler handler = new  AssetServerGetHandler(null);
            BaseRequestHandlerTestHelper.BaseTestGetParams(handler, ASSETS_PATH);
        }

        [Test]
        public void TestSplitParams()
        {
            TestHelper.InMethod();

            AssetServerGetHandler handler = new  AssetServerGetHandler(null);
            BaseRequestHandlerTestHelper.BaseTestSplitParams(handler, ASSETS_PATH);
        }

        //[Test]
        //public void TestHandleNoParams()
        //{
        //    TestHelper.InMethod();

        //    AssetServerGetHandler handler = new AssetServerGetHandler(null);

        //    BaseRequestHandlerTestHelper.BaseTestHandleNoParams(handler, ASSETS_PATH);
        //}

        //[Test]
        //public void TestHandleMalformedGuid()
        //{
        //    TestHelper.InMethod();

        //    AssetServerGetHandler handler = new AssetServerGetHandler(null);

        //    BaseRequestHandlerTestHelper.BaseTestHandleMalformedGuid(handler, ASSETS_PATH);
        //}

        //[Test]
        //public void TestHandleFetchMissingAsset()
        //{
        //    IAssetService assetDataPlugin = new TestAssetDataPlugin();
        //    AssetServerGetHandler handler = new AssetServerGetHandler(assetDataPlugin);

        //    GetAssetStreamHandlerTestHelpers.BaseFetchMissingAsset(handler);
        //}

        //[Test]
        //public void TestHandleFetchExistingAssetData()
        //{
        //    AssetServerGetHandler handler;
        //    OSHttpResponse response;
        //    AssetBase asset = CreateTestEnvironment(out handler, out response);

        //    GetAssetStreamHandlerTestHelpers.BaseFetchExistingAssetDataTest(asset, handler, response);
        //}

        //[Test]
        //public void TestHandleFetchExistingAssetXml()
        //{
        //    AssetServerGetHandler handler;
        //    OSHttpResponse response;
        //    AssetBase asset = CreateTestEnvironment(out handler, out response);

        //    GetAssetStreamHandlerTestHelpers.BaseFetchExistingAssetXmlTest(asset, handler, response);
        //}

        private static AssetBase CreateTestEnvironment(out  AssetServerGetHandler handler, out OSHttpResponse response)
        {
            AssetBase asset = GetAssetStreamHandlerTestHelpers.CreateCommonTestResources(out response);

            IAssetService assetDataPlugin = new TestAssetService();
            handler = new  AssetServerGetHandler(assetDataPlugin);

            assetDataPlugin.Store(asset);
            return asset;
        }
    }
}