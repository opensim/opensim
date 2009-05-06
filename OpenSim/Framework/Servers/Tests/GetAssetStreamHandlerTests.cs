using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;

namespace OpenSim.Framework.Servers.Tests
{
    [TestFixture]
    public class GetAssetStreamHandlerTests
    {
        [Test]
        public void TestConstructor()
        {
            GetAssetStreamHandler handler = new GetAssetStreamHandler( null );
        }

        [Test]
        public void TestGetParams()
        {
            GetAssetStreamHandler handler = new GetAssetStreamHandler(null);

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
            GetAssetStreamHandler handler = new GetAssetStreamHandler(null);

            Assert.AreEqual(new string[] { }, handler.SplitParams("/assets"), "Failed on empty params.");
            Assert.AreEqual(new string[] { }, handler.SplitParams("/assets/"), "Failed on single slash.");
            Assert.AreEqual(new string[] { "a" }, handler.SplitParams("/assets/a"), "Failed on first segment.");
            Assert.AreEqual(new string[] { "b" }, handler.SplitParams("/assets/b/"), "Failed on second slash.");
        }

    }
}
