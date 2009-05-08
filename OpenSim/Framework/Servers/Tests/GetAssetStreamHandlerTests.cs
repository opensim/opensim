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

        //[Test]
        //public void TestHandleFetchMissingAsset()
        //{
        //    byte[] emptyResult = new byte[] { };
        //    GetAssetStreamHandler handler = new GetAssetStreamHandler(null);

        //    Assert.AreEqual(new string[] { }, handler.Handle("/assets/badGuid", null, null, null), "Failed on bad guid.");
        //}
    }
}
