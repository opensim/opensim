using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;

namespace OpenSim.Tests.Common
{
    public class BaseRequestHandlerTestHelper
    {
        private static string[] m_emptyStringArray = new string[] { };

        public static void BaseTestGetParams(BaseRequestHandler handler, string assetsPath)
        {
            Assert.AreEqual(String.Empty, handler.GetParam(null), "Failed on null path.");
            Assert.AreEqual(String.Empty, handler.GetParam(""), "Failed on empty path.");
            Assert.AreEqual(String.Empty, handler.GetParam("s"), "Failed on short url.");
            Assert.AreEqual(String.Empty, handler.GetParam("corruptUrl"), "Failed on corruptUrl.");

            Assert.AreEqual(String.Empty, handler.GetParam(assetsPath));
            Assert.AreEqual("/", handler.GetParam(assetsPath + "/"));
            Assert.AreEqual("/a", handler.GetParam(assetsPath + "/a"));
            Assert.AreEqual("/b/", handler.GetParam(assetsPath + "/b/"));
            Assert.AreEqual("/c/d", handler.GetParam(assetsPath + "/c/d"));
            Assert.AreEqual("/e/f/", handler.GetParam(assetsPath + "/e/f/"));
        }

        public static void BaseTestSplitParams(BaseRequestHandler handler, string assetsPath)
        {
            Assert.AreEqual(m_emptyStringArray, handler.SplitParams(null), "Failed on null.");
            Assert.AreEqual(m_emptyStringArray, handler.SplitParams(""), "Failed on empty path.");
            Assert.AreEqual(m_emptyStringArray, handler.SplitParams("corruptUrl"), "Failed on corrupt url.");
 
            Assert.AreEqual(m_emptyStringArray, handler.SplitParams(assetsPath), "Failed on empty params.");
            Assert.AreEqual(m_emptyStringArray, handler.SplitParams(assetsPath + "/"), "Failed on single slash.");

            Assert.AreEqual(new string[] { "a" }, handler.SplitParams(assetsPath + "/a"), "Failed on first segment.");
            Assert.AreEqual(new string[] { "b" }, handler.SplitParams(assetsPath + "/b/"), "Failed on second slash.");
            Assert.AreEqual(new string[] { "c", "d" }, handler.SplitParams(assetsPath + "/c/d"), "Failed on second segment.");
            Assert.AreEqual(new string[] { "e", "f" }, handler.SplitParams(assetsPath + "/e/f/"), "Failed on trailing slash.");
        }
    }
}
