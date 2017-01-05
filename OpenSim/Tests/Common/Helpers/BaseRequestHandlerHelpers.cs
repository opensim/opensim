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
using System.Text;
using NUnit.Framework;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;

namespace OpenSim.Tests.Common
{
    public class BaseRequestHandlerHelpers
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

        public static byte[] EmptyByteArray = new byte[] {};

    }
}
