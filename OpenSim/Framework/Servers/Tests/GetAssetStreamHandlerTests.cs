using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using OpenSim.Data;
using OpenSim.Tests.Common;

namespace OpenSim.Framework.Servers.Tests
{
    [TestFixture]
    public class GetAssetStreamHandlerTests
    {      
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

            Assert.AreEqual("", handler.GetParam(null), "Failed on null path.");
            Assert.AreEqual("", handler.GetParam(""), "Failed on empty path.");
            Assert.AreEqual("", handler.GetParam("s"), "Failed on short url.");
            Assert.AreEqual("", handler.GetParam("corruptUrl"), "Failed on corruptUrl.");

            Assert.AreEqual("", handler.GetParam("/assets"));
            Assert.AreEqual("/", handler.GetParam("/assets/"));
            Assert.AreEqual("/a", handler.GetParam("/assets/a"));
            Assert.AreEqual("/b/", handler.GetParam("/assets/b/"));
            Assert.AreEqual("/c/d", handler.GetParam("/assets/c/d"));
            Assert.AreEqual("/e/f/", handler.GetParam("/assets/e/f/"));
        }

        [Test]
        public void TestSplitParams()
        {
            TestHelper.InMethod();

            GetAssetStreamHandler handler = new GetAssetStreamHandler(null);

            Assert.AreEqual(new string[] { }, handler.SplitParams(null), "Failed on null.");
            Assert.AreEqual(new string[] { }, handler.SplitParams(""), "Failed on empty path.");
            Assert.AreEqual(new string[] { }, handler.SplitParams("corruptUrl"), "Failed on corrupt url.");
 
            Assert.AreEqual(new string[] { }, handler.SplitParams("/assets"), "Failed on empty params.");
            Assert.AreEqual(new string[] { }, handler.SplitParams("/assets/"), "Failed on single slash.");
            Assert.AreEqual(new string[] { "a" }, handler.SplitParams("/assets/a"), "Failed on first segment.");
            Assert.AreEqual(new string[] { "b" }, handler.SplitParams("/assets/b/"), "Failed on second slash.");
            Assert.AreEqual(new string[] { "c", "d" }, handler.SplitParams("/assets/c/d"), "Failed on second segment.");
            Assert.AreEqual(new string[] { "e", "f" }, handler.SplitParams("/assets/e/f/"), "Failed on trailing slash.");
        }

        [Test]
        public void TestHandleNoParams()
        {
            TestHelper.InMethod();

            byte[] emptyResult = new byte[] {};
            GetAssetStreamHandler handler = new GetAssetStreamHandler(null);

            Assert.AreEqual(new string[] { }, handler.Handle("/assets", null, null, null), "Failed on empty params.");
            Assert.AreEqual(new string[] { }, handler.Handle("/assets/", null, null, null ), "Failed on single slash.");
        }

        [Test]
        public void TestHandleMalformedGuid()
        {
            TestHelper.InMethod();

            byte[] emptyResult = new byte[] {};
            GetAssetStreamHandler handler = new GetAssetStreamHandler(null);

            Assert.AreEqual(new string[] {}, handler.Handle("/assets/badGuid", null, null, null), "Failed on bad guid.");
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
