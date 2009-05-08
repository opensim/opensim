using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using OpenSim.Data;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Tests.Common;

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

        //[Test]
        //public void TestHandleFetchMissingAsset()
        //{

        //    byte[] emptyResult = new byte[] { };
        //    CachedGetAssetStreamHandler handler = new CachedGetAssetStreamHandler(null);

        //    Assert.AreEqual(new string[] { }, handler.Handle("/assets/badGuid", null, null, null), "Failed on bad guid.");
        //}
    }
}
